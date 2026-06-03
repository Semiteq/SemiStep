using System.Reactive.Linq;

using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.S7.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

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

	private static (
		PlcSyncCoordinator Coordinator,
		FakeS7Transport Transport,
		StubS7ServiceForSync ConnectionService) Build(bool connected = false)
	{
		var transport = new FakeS7Transport();
		transport.SetConnected(connected);

		var connectionService = new StubS7ServiceForSync(connected);
		var converter = new RecipeConverter(BuildMinimalRecipeMetadataRegistry());
		var configuration = BuildTestConfiguration();
		var arrayCodec = TestArrayCodecFactory.Create(configuration);
		var executor = new PlcTransactionExecutor(
			transport, converter, arrayCodec, configuration, NullLogger<PlcTransactionExecutor>.Instance);
		var coordinator = new PlcSyncCoordinator(
			executor, connectionService, NullLoggerFactory.Instance);

		return (coordinator, transport, connectionService);
	}

	private static RecipeMetadataRegistry BuildMinimalRecipeMetadataRegistry()
	{
		var config = new AppConfiguration(
			Properties: TestRecipeMetadataRegistryFactory.DefaultStringProperty(),
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

		// Wait for the SUT-internal 1000 ms debounce to elapse and the resulting sync to complete.
		await coordinator.WaitForPendingSyncAsync(TestContext.Current.CancellationToken);

		// (a) Observable state: WaitForPendingSyncAsync returns when the pending sync is scheduled to complete,
		// but PlcState propagation through the Rx pipeline is not strictly synchronous —
		// poll the observable side effect.
		await TestHelpers.WaitUntilAsync(
			() => transport.WriteLog.Count > 0,
			timeout: TimeSpan.FromMilliseconds(2500),
			pollInterval: TimeSpan.FromMilliseconds(20),
			cancellationToken: TestContext.Current.CancellationToken);

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

		await coordinator.WaitForPendingSyncAsync(TestContext.Current.CancellationToken);

		// (a) Observable state: poll Status until the sync pipeline transitions to Synced.
		await TestHelpers.WaitUntilAsync(
			() => coordinator.Status == PlcSyncStatus.Synced,
			timeout: TimeSpan.FromMilliseconds(2500),
			pollInterval: TimeSpan.FromMilliseconds(20),
			cancellationToken: TestContext.Current.CancellationToken);

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
		await coordinator.WaitForPendingSyncAsync(TestContext.Current.CancellationToken);

		// (a) Observable state: poll LastSyncTime until the sync completion stamps it.
		await TestHelpers.WaitUntilAsync(
			() => coordinator.LastSyncTime is not null,
			timeout: TimeSpan.FromMilliseconds(2500),
			pollInterval: TimeSpan.FromMilliseconds(20),
			cancellationToken: TestContext.Current.CancellationToken);

		coordinator.LastSyncTime.Should().NotBeNull();
		coordinator.LastSyncTime!.Value.Should().BeOnOrAfter(before);
	}

	[Fact]
	public void Dispose_PreventsSubsequentNotifications()
	{
		var (coordinator, _, _) = Build(connected: false);
		coordinator.Dispose();

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: false);

		coordinator.Status.Should().Be(PlcSyncStatus.Idle,
			"after disposal, notifications must be ignored");
	}

	[Fact]
	public void ReEnableAfterCleanDisable_DoesNotEmitConnectionLostFailure()
	{
		var (coordinator, _, _) = Build(connected: false);
		Result<PlcSessionSnapshot>? latest = null;
		using var subscription = coordinator.PlcState.Subscribe(snapshot => latest = snapshot);

		coordinator.SetSyncEnabled(true);
		coordinator.SetSyncEnabled(false);
		coordinator.ResetForDisable();
		coordinator.SetSyncEnabled(true);

		latest.Should().NotBeNull();
		latest!.IsFailed.Should().BeFalse(
			"re-enabling sync after a clean disable must not emit a spurious connection-lost failure");
	}

	[Fact]
	public void ReEnableAfterConnectionLostThenCleanDisable_DoesNotEmitConnectionLostFailure()
	{
		var (coordinator, _, _) = Build(connected: false);
		Result<PlcSessionSnapshot>? latest = null;
		using var subscription = coordinator.PlcState.Subscribe(snapshot => latest = snapshot);

		// Drive the coordinator into the stale state that produced the field bug: a genuine
		// connection loss leaves Status = Disconnected. The user then toggles Sync off (clean
		// disable) and back on. ResetForDisable must clear the stale status to Idle so the
		// re-enable does not republish the connection-lost failure.
		coordinator.SetSyncEnabled(true);
		coordinator.HandleConnectionLost();
		coordinator.SetSyncEnabled(false);
		coordinator.ResetForDisable();
		coordinator.SetSyncEnabled(true);

		latest.Should().NotBeNull();
		latest!.IsFailed.Should().BeFalse(
			"after a genuine loss is cleared by a clean disable, re-enabling sync must not "
			+ "republish the stale connection-lost failure");
	}

	[Fact]
	public void HandleConnectionLost_WhileSyncEnabled_EmitsConnectionLostFailure()
	{
		var (coordinator, _, _) = Build(connected: false);
		coordinator.SetSyncEnabled(true);

		Result<PlcSessionSnapshot>? latest = null;
		using var subscription = coordinator.PlcState.Subscribe(snapshot => latest = snapshot);

		coordinator.HandleConnectionLost();

		latest.Should().NotBeNull();
		latest!.IsFailed.Should().BeTrue(
			"a runtime connection loss while sync is enabled must surface as a failed snapshot");
	}

	[Fact]
	public void ReEnableAfterConnectionLost_ClearsLostFlag_DoesNotEmitFailure()
	{
		var (coordinator, _, _) = Build(connected: false);
		coordinator.SetSyncEnabled(true);

		Result<PlcSessionSnapshot>? latest = null;
		using var subscription = coordinator.PlcState.Subscribe(snapshot => latest = snapshot);

		coordinator.HandleConnectionLost();
		latest!.IsFailed.Should().BeTrue("a connection loss while enabled must surface as a failure first");

		coordinator.SetSyncEnabled(true);

		latest.Should().NotBeNull();
		latest!.IsFailed.Should().BeFalse(
			"re-enabling sync must clear the connection-lost flag so no stale failure lingers");
	}

	[Fact]
	public void ConnectedAfterConnectionLost_ClearsLostFlag_DoesNotEmitFailure()
	{
		var (coordinator, _, _) = Build(connected: false);
		coordinator.SetSyncEnabled(true);

		Result<PlcSessionSnapshot>? latest = null;
		using var subscription = coordinator.PlcState.Subscribe(snapshot => latest = snapshot);

		coordinator.HandleConnectionLost();
		latest!.IsFailed.Should().BeTrue("a connection loss while enabled must surface as a failure first");

		coordinator.UpdateConnectionState(PlcConnectionState.Connected);

		latest.Should().NotBeNull();
		latest!.IsFailed.Should().BeFalse(
			"the auto-reconnect recovery path must clear the connection-lost flag without the user toggling sync");
	}

	[Fact]
	public async Task NotifyRecipeChanged_IsValidTrue_Disconnected_DebounceAbortLeavesStatusUnchanged()
	{
		var (coordinator, transport, _) = Build(connected: false);

		coordinator.NotifyRecipeChanged(Recipe.Empty, isValid: true);

		// The debounce elapses, but CheckCanSyncAsync aborts the sync because the PLC is not connected.
		await coordinator.WaitForPendingSyncAsync(TestContext.Current.CancellationToken);

		coordinator.Status.Should().Be(PlcSyncStatus.Idle,
			"a debounce that fires while disconnected must abort cleanly without transitioning to Failed");
		transport.WriteLog.Should().BeEmpty(
			"no write may reach the PLC while disconnected");
	}
}
