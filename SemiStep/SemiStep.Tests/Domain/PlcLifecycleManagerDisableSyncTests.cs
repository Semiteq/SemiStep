using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Domain;

[Trait("Component", "Domain")]
[Trait("Area", "SyncDisable")]
[Trait("Category", "Integration")]
public sealed class PlcLifecycleManagerDisableSyncTests
{
	private static readonly TimeSpan _disableBoundWait = TimeSpan.FromSeconds(10);

	private static CancellationToken TestToken => TestContext.Current.CancellationToken;

	private sealed record Harness(
		PlcLifecycleManager Plc,
		StubS7Service S7Service,
		StubPlcSyncService SyncService,
		StubPlcSyncOwnership Ownership,
		RecordingLogger<PlcLifecycleManager> Logger);

	private static async Task<Harness> BuildEnabledAsync()
	{
		var configDir = TestConfigLocator.GetConfigDirectory("Standard");
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var s7Service = new StubS7Service();
		var syncService = new StubPlcSyncService();
		var ownership = new StubPlcSyncOwnership();
		var logger = new RecordingLogger<PlcLifecycleManager>();

		var services = new ServiceCollection()
			.AddLogging()
			.AddSingleton<ILogger<PlcLifecycleManager>>(logger)
			.AddSingleton(configLoadResult.Value)
			.AddRecipe()
			.AddCsv()
			.AddClipboard()
			.AddSingleton<IS7Connection>(s7Service)
			.AddSingleton<IS7Reader>(s7Service)
			.AddSingleton<IS7ExecutionStream>(s7Service)
			.AddSingleton<IPlcSyncService>(syncService)
			.AddSingleton<IPlcSyncOwnership>(ownership)
			.BuildServiceProvider();

		services.GetRequiredService<RecipeSession>().Reset();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		(await plc.EnableSync(PlcConfiguration.Default)).IsSuccess.Should().BeTrue();

		return new Harness(plc, s7Service, syncService, ownership, logger);
	}

	private static int Warnings(Harness harness)
	{
		return harness.Logger.Snapshot().Count(entry => entry.Level == LogLevel.Warning);
	}

	[Fact]
	public async Task DisableSync_WithPendingSync_DisconnectsAndReleasesOnlyAfterItEnds()
	{
		var harness = await BuildEnabledAsync();
		var pendingSync = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		harness.SyncService.PendingSync = pendingSync.Task;
		var lease = harness.Ownership.LastLease!;
		int? leaseDisposalsAtDisconnect = null;
		harness.S7Service.OnDisconnect = () => leaseDisposalsAtDisconnect = lease.DisposeCallCount;
		harness.SyncService.WasResetForDisableCalled.Should().BeFalse();

		var disableTask = harness.Plc.DisableSync();

		disableTask.IsCompleted.Should().BeFalse("DisableSync must wait for the write in flight");
		harness.SyncService.WasResetForDisableCalled.Should().BeTrue("the reset must run before the wait");
		harness.S7Service.DisconnectCallCount.Should().Be(0, "the disconnect must not tear the write in flight");
		lease.DisposeCallCount.Should().Be(0, "the lease must stay held while our write runs");

		pendingSync.SetResult();
		await disableTask.WaitAsync(TestToken);

		harness.S7Service.DisconnectCallCount.Should().Be(1);
		leaseDisposalsAtDisconnect.Should().Be(0, "the lease must stay held until the session ends");
		lease.DisposeCallCount.Should().Be(1);
		Warnings(harness).Should().Be(0, "a write that ends inside the bound is not a warning");
	}

	[Fact]
	public async Task DisableSync_WithPendingSyncPastTheBound_DisconnectsAndLogsOneWarning()
	{
		var harness = await BuildEnabledAsync();
		harness.SyncService.PendingSync = new TaskCompletionSource().Task;

		await harness.Plc.DisableSync().WaitAsync(_disableBoundWait, TestToken);

		harness.S7Service.DisconnectCallCount.Should().Be(1, "an expired bound must still disconnect");
		harness.Ownership.LastLease!.DisposeCallCount.Should().Be(1);
		Warnings(harness).Should().Be(1, "the expired bound is logged once");
	}

	[Fact]
	public async Task DisableSync_WhenDisconnectThrows_StillReleasesTheLease()
	{
		var harness = await BuildEnabledAsync();
		harness.S7Service.DisconnectShouldFail = true;

		await harness.Plc.DisableSync();

		harness.S7Service.DisconnectCallCount.Should().Be(1);
		harness.Ownership.LastLease!.DisposeCallCount.Should().Be(1,
			"a failed disconnect must not orphan the ownership lease");
	}
}
