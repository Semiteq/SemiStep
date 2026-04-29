using System.Reactive.Linq;

using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;

using Tests.S7.Helpers;

using Xunit;

namespace Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "SyncCoordinator")]
[Trait("Category", "Unit")]
public sealed class PlcSyncCoordinatorTests
{
	private static PlcConfiguration BuildTestConfiguration()
	{
		var layout = new PlcProtocolLayout(
			ManagingDb: ManagingDbLayout.Default,
			IntDb: new DataDbLayout(DbNumber: 3, CapacityOffset: 0, CurrentSizeOffset: 4, DataStartOffset: 8),
			FloatDb: new DataDbLayout(DbNumber: 4, CapacityOffset: 0, CurrentSizeOffset: 4, DataStartOffset: 8),
			StringDb: new DataDbLayout(DbNumber: 5, CapacityOffset: 0, CurrentSizeOffset: 4, DataStartOffset: 8),
			ExecutionDb: ExecutionDbLayout.Default);

		return new PlcConfiguration(
			PlcConnectionSettings.Default,
			PlcProtocolSettings.Default,
			layout);
	}

	private static (PlcSyncCoordinator Coordinator, FakeS7Transport Transport, StubS7ServiceForSync ConnectionService) Build(
		bool connected = false)
	{
		var transport = new FakeS7Transport();
		transport.SetConnected(connected);

		var connectionService = new StubS7ServiceForSync(connected);
		var converter = new RecipeConverter(BuildEmptyRecipeMetadataRegistry());
		var configuration = BuildTestConfiguration();
		var executor = new PlcTransactionExecutor(
			transport, converter, configuration, NullLogger<PlcTransactionExecutor>.Instance);
		var coordinator = new PlcSyncCoordinator(
			executor, connectionService, NullLoggerFactory.Instance);

		return (coordinator, transport, connectionService);
	}

	private static RecipeMetadataRegistry BuildEmptyRecipeMetadataRegistry()
	{
		var config = new AppConfiguration(
			Properties: new Dictionary<string, PropertyTypeDefinition>(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default);

		return new RecipeMetadataRegistry(config);
	}

	[Fact]
	public void NotifyRecipeChanged_IsValidFalse_SetsStatusOutOfSync()
	{
		var (coordinator, _, _) = Build();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);

		coordinator.Status.Should().Be(PlcSyncStatus.OutOfSync);
	}

	[Fact]
	public void NotifyRecipeChanged_IsValidFalse_DoesNotScheduleWrite()
	{
		var (coordinator, transport, _) = Build(connected: false);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);

		transport.WriteLog.Should().BeEmpty(
			"an invalid recipe must never trigger a write to the PLC");
	}

	[Fact]
	public void NotifyRecipeChanged_IsValidFalse_EmitsOutOfSyncSnapshot()
	{
		var (coordinator, _, _) = Build();
		Result<PlcSessionSnapshot>? received = null;
		using var sub = coordinator.PlcState.Skip(1).Take(1).Subscribe(s => received = s);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);

		received.Should().NotBeNull();
		received!.Value.SyncStatus.Should().Be(PlcSyncStatus.OutOfSync);
	}

	[Fact]
	public void NotifyRecipeChanged_IsValidFalse_StatusChangedMultipleTimes_OnlyEmitsWhenValueChanges()
	{
		var (coordinator, _, _) = Build();
		var snapshots = new List<Result<PlcSessionSnapshot>>();

		// Skip the initial state emitted on subscription.
		using var sub = coordinator.PlcState.Skip(1).Subscribe(snapshots.Add);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);
		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);

		snapshots.Should().HaveCount(1,
			"PlcState must not emit a new snapshot when the status value has not changed");
	}

	[Fact]
	public void NotifyRecipeChanged_IsValidTrue_StatusRemainsIdle_WhenNotConnected()
	{
		var (coordinator, _, _) = Build(connected: false);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);

		coordinator.Status.Should().Be(PlcSyncStatus.Idle,
			"debounce is queued but sync does not execute when disconnected");
	}

	[Fact]
	public async Task NotifyRecipeChanged_IsValidTrue_Connected_ExecutesSyncAfterDebounce()
	{
		var (coordinator, transport, connectionService) = Build(connected: true);
		connectionService.SetConnected(true);

		// Configure read-back for verification (empty arrays)
		var layout = BuildTestConfiguration().Layout;
		transport.SetReadResponseForDb(layout.IntDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.FloatDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.StringDb.DbNumber, (_, count) => new byte[count]);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);

		// Wait for debounce (1000 ms) plus a generous margin.
		await coordinator.WaitForPendingSyncAsync();
		await Task.Delay(1200);

		transport.WriteLog.Should().NotBeEmpty(
			"after debounce period, a valid recipe should have been written to the PLC");
	}

	[Fact]
	public async Task NotifyRecipeChanged_IsValidTrue_Connected_EventuallySetsStatusSynced()
	{
		var (coordinator, transport, connectionService) = Build(connected: true);

		var layout = BuildTestConfiguration().Layout;
		transport.SetReadResponseForDb(layout.IntDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.FloatDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.StringDb.DbNumber, (_, count) => new byte[count]);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);

		await coordinator.WaitForPendingSyncAsync();
		await Task.Delay(1200);

		coordinator.Status.Should().Be(PlcSyncStatus.Synced);
	}

	[Fact]
	public async Task NotifyRecipeChanged_IsValidTrue_Connected_SetsLastSyncTime()
	{
		var (coordinator, transport, connectionService) = Build(connected: true);

		var layout = BuildTestConfiguration().Layout;
		transport.SetReadResponseForDb(layout.IntDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.FloatDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.StringDb.DbNumber, (_, count) => new byte[count]);

		var before = DateTimeOffset.UtcNow;
		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await coordinator.WaitForPendingSyncAsync();
		await Task.Delay(1200);

		coordinator.LastSyncTime.Should().NotBeNull();
		coordinator.LastSyncTime!.Value.Should().BeOnOrAfter(before);
	}

	[Fact]
	public void Dispose_PreventsSubsequentNotifications()
	{
		var (coordinator, transport, _) = Build(connected: false);
		coordinator.Dispose();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);

		coordinator.Status.Should().Be(PlcSyncStatus.Idle,
			"after disposal, notifications must be ignored");
	}
}
