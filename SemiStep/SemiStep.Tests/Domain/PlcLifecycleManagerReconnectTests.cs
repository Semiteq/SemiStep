using System.Collections.Immutable;

using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Domain;

[Trait("Component", "Domain")]
[Trait("Area", "Reconnect")]
[Trait("Category", "Integration")]
public sealed class PlcLifecycleManagerReconnectTests
{
	private const int WaitActionId = 10;

	private static async Task<(
		PlcLifecycleManager Plc,
		RecipeSession Session,
		StubS7Service S7Service,
		StubPlcSyncService SyncService)> BuildAsync()
	{
		var configDir = TestConfigLocator.GetConfigDirectory("Standard");
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var s7Service = new StubS7Service();
		var syncService = new StubPlcSyncService();

		var services = new ServiceCollection()
			.AddLogging()
			.AddSingleton(configLoadResult.Value)
			.AddRecipe()
			.AddCsv()
			.AddClipboard()
			.AddSingleton<IS7Connection>(s7Service)
			.AddSingleton<IS7Reader>(s7Service)
			.AddSingleton<IS7ExecutionStream>(s7Service)
			.AddSingleton<IPlcSyncService>(syncService)
			.AddSingleton<IPlcSyncOwnership, StubPlcSyncOwnership>()
			.BuildServiceProvider();

		var session = services.GetRequiredService<RecipeSession>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		session.Reset();

		return (plc, session, s7Service, syncService);
	}

	private static Recipe BuildSingleStepRecipe()
	{
		var step = new Step(
			WaitActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty);

		return new Recipe(ImmutableList.Create(step));
	}

	[Fact]
	public async Task StateChanged_Connected_WhenRecipesDiffer_FiresConflictDetected()
	{
		var (plc, session, s7Service, _) = await BuildAsync();

		// Populate local recipe so it is non-empty.
		var appendResult = session.AppendStep(WaitActionId);
		appendResult.IsSuccess.Should().BeTrue();

		// Configure stub: committed=true, PLC recipe different from local.
		var plcRecipe = BuildSingleStepRecipe();
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = plcRecipe;

		// Activate sync so the relay handles Connected events.
		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		Recipe? conflictLocalRecipe = null;
		Recipe? conflictPlcRecipe = null;
		plc.PlcRecipeConflictDetected += (local, plcRec) =>
		{
			conflictLocalRecipe = local;
			conflictPlcRecipe = plcRec;
		};

		// Simulate an auto-reconnect: StateChanged fires Connected while sync is active.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		// (a) Observable state: wait until the fire-and-forget reconciliation has raised the conflict event.
		await TestHelpers.WaitUntilAsync(
			() => conflictLocalRecipe is not null,
			cancellationToken: TestContext.Current.CancellationToken);

		conflictLocalRecipe.Should().NotBeNull(
			"PlcRecipeConflictDetected must fire when local and PLC recipes differ and both are non-empty");
		conflictPlcRecipe.Should().Be(plcRecipe);
	}

	[Fact]
	public async Task StateChanged_Connected_WhenRecipesIdentical_DoesNotFireConflict_AndPushesLocal()
	{
		var (plc, session, s7Service, syncService) = await BuildAsync();

		// Populate local recipe so it is non-empty; AppendStep populates default properties
		// per the action definition, so the local step is not empty.
		var appendResult = session.AppendStep(WaitActionId);
		appendResult.IsSuccess.Should().BeTrue();

		// Configure stub: committed=true, PLC recipe is a fresh-instance deep copy of the local one.
		// Reusing BuildSingleStepRecipe() would yield empty Properties, which differ from the
		// session's populated step and would wrongly trip the conflict branch.
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = DeepCopy(session.Current);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		var conflictFired = false;
		plc.PlcRecipeConflictDetected += (_, _) => conflictFired = true;

		var countBeforeStateChange = syncService.NotifyRecipeChangedCallCount;

		// Simulate an auto-reconnect: StateChanged fires Connected while sync is active.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		// (a) Observable state: identical content takes the equal branch, which falls through
		// to NotifyLocalRecipe and increments the counter.
		await TestHelpers.WaitUntilAsync(
			() => syncService.NotifyRecipeChangedCallCount > countBeforeStateChange,
			cancellationToken: TestContext.Current.CancellationToken);

		conflictFired.Should().BeFalse(
			"identical PLC and PC recipe content must not raise PlcRecipeConflictDetected");
	}

	private static Recipe DeepCopy(Recipe recipe)
	{
		var copiedSteps = recipe.Steps
			.Select(step => new Step(step.ActionKey, ImmutableDictionary.CreateRange(step.Properties)));

		return new Recipe(ImmutableList.CreateRange(copiedSteps));
	}

	[Fact]
	public async Task StateChanged_Connected_WhenNotCommitted_PushesLocalRecipe()
	{
		var (plc, _, s7Service, syncService) = await BuildAsync();

		// Configure stub: committed=false, so reconciliation should push local recipe.
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: false, RecipeLines: 0);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		// Capture the call count after EnableSync to measure only the reconnect-triggered push.
		var countBeforeStateChange = syncService.NotifyRecipeChangedCallCount;

		// Simulate an auto-reconnect.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		// (a) Observable state: wait until the reconcile-on-reconnect pushes the local recipe.
		await TestHelpers.WaitUntilAsync(
			() => syncService.NotifyRecipeChangedCallCount > countBeforeStateChange,
			cancellationToken: TestContext.Current.CancellationToken);

		syncService.NotifyRecipeChangedCallCount.Should().BeGreaterThan(countBeforeStateChange,
			"when committed=false the manager must push the local recipe to the PLC via NotifyRecipeChanged");
	}

	[Fact]
	public async Task StateChanged_Disconnected_WhenSyncEnabled_CallsHandleConnectionLost()
	{
		var (plc, _, s7Service, syncService) = await BuildAsync();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		// Simulate a connection drop while sync is active.
		s7Service.RaiseStateChanged(PlcConnectionState.Disconnected);

		syncService.WasHandleConnectionLostCalled.Should().BeTrue(
			"IPlcSyncService.HandleConnectionLost() must be called when the PLC disconnects while sync is enabled");
		syncService.WasResetForDisableCalled.Should().BeFalse(
			"a connection drop is a loss alarm, not a clean teardown, so it must not take the disable path");
	}

	[Fact]
	public async Task DisableSync_CallsResetForDisableOnSyncService()
	{
		var (plc, _, _, syncService) = await BuildAsync();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		await plc.DisableSync();

		syncService.WasHandleConnectionLostCalled.Should().BeFalse(
			"manual disable is a clean teardown, not a connection-loss alarm");
		syncService.WasResetForDisableCalled.Should().BeTrue(
			"IPlcSyncService.ResetForDisable() must be called when sync is manually disabled");
	}

	[Fact]
	public async Task StateChanged_Connected_WhenLocalEmptyAndPlcNonEmpty_InvokesReconnectApplyCallback()
	{
		var (plc, _, s7Service, _) = await BuildAsync();

		var plcRecipe = BuildSingleStepRecipe();
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = plcRecipe;

		Recipe? appliedRecipe = null;
		plc.RegisterReconnectApplyCallback(recipe =>
		{
			appliedRecipe = recipe;
			return Task.FromResult(Result.Ok());
		});

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await TestHelpers.WaitUntilAsync(
			() => appliedRecipe is not null,
			cancellationToken: TestContext.Current.CancellationToken);

		appliedRecipe.Should().Be(plcRecipe,
			"reconnect reconciliation must route the PLC recipe through the registered callback "
			+ "so the coordinator can marshal to the UI thread and dispatch the mutation signal");
	}

	[Fact]
	public async Task StateChanged_Connected_WhenLocalEmptyAndPlcNonEmpty_DoesNotMutateSessionDirectly()
	{
		var (plc, session, s7Service, _) = await BuildAsync();

		var plcRecipe = BuildSingleStepRecipe();
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = plcRecipe;

		// Register a no-op callback so the path completes without applying anything to the session.
		// The session must remain empty: the lifecycle manager is forbidden from mutating it directly,
		// because such a mutation skips UI-thread marshalling and the mutation-signal channel.
		var callbackInvoked = false;
		plc.RegisterReconnectApplyCallback(_ =>
		{
			callbackInvoked = true;
			return Task.FromResult(Result.Ok());
		});

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await TestHelpers.WaitUntilAsync(
			() => callbackInvoked,
			cancellationToken: TestContext.Current.CancellationToken);

		session.Current.StepCount.Should().Be(0,
			"the lifecycle manager must delegate session mutation to the registered callback; "
			+ "direct mutation would silently desynchronize the grid VM from the session");
	}

	[Fact]
	public async Task EnableSync_WhenProtocolVersionMatches_Succeeds()
	{
		var (plc, _, s7Service, syncService) = await BuildAsync();
		s7Service.ProtocolVersionToReturn = Result.Ok(1);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);

		enableResult.IsSuccess.Should().BeTrue(
			"a matching protocol version must let the handshake proceed");
		syncService.IsSyncEnabled.Should().BeTrue();
		s7Service.DisconnectCallCount.Should().Be(0,
			"a successful handshake must not disconnect");
	}

	[Fact]
	public async Task EnableSync_WhenProtocolVersionMismatches_FailsAndDisconnectsAndLeavesSyncDisabled()
	{
		var (plc, _, s7Service, syncService) = await BuildAsync();
		s7Service.ProtocolVersionToReturn = Result.Ok(2);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);

		enableResult.IsFailed.Should().BeTrue("a version mismatch must fail EnableSync");
		enableResult.HasError<ProtocolVersionMismatchError>().Should().BeTrue(
			"the failure reason must be a typed ProtocolVersionMismatchError");
		syncService.IsSyncEnabled.Should().BeFalse(
			"the sync flag must be cleared before disconnect on a mismatch");
		s7Service.DisconnectCallCount.Should().Be(1,
			"a mismatch must disconnect from the PLC");
		s7Service.ReadManagingAreaCallCount.Should().Be(0,
			"no managing-area read or recipe apply may occur on a version mismatch");
	}

	[Fact]
	public async Task EnableSync_WhenVersionReadFails_FailsAndDisconnects()
	{
		var (plc, _, s7Service, syncService) = await BuildAsync();
		s7Service.ProtocolVersionToReturn = Result.Fail<int>("version read failed");

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);

		enableResult.IsFailed.Should().BeTrue("a failed version read must fail EnableSync");
		enableResult.HasError<ProtocolVersionMismatchError>().Should().BeFalse(
			"a read failure must propagate the read error, not be reported as a version mismatch");
		syncService.IsSyncEnabled.Should().BeFalse();
		s7Service.DisconnectCallCount.Should().Be(1);
		s7Service.ReadManagingAreaCallCount.Should().Be(0,
			"no managing-area read or recipe apply may occur when the version read fails");
	}

	[Fact]
	public async Task EnableSync_WhenConnectedFiresDuringConnect_AndVersionMismatches_DoesNotReadOrApply()
	{
		var (plc, _, s7Service, _) = await BuildAsync();

		// Mirror the real S7Service: Connected is published synchronously inside ConnectAsync,
		// triggering reconnect reconciliation before the EnableSync version handshake finishes.
		s7Service.RaiseConnectedDuringConnect = true;
		s7Service.ProtocolVersionToReturn = Result.Ok(2);
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = BuildSingleStepRecipe();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);

		enableResult.IsFailed.Should().BeTrue("a version mismatch must fail EnableSync");
		enableResult.HasError<ProtocolVersionMismatchError>().Should().BeTrue();

		// Wait until both the EnableSync handshake and the fire-and-forget reconciliation triggered
		// during connect have disconnected on the version mismatch. Both abort via the same
		// FailProtocolVersionHandshakeAsync path that disconnects before reading the managing area,
		// so two disconnects deterministically signal that reconciliation has fully aborted.
		await TestHelpers.WaitUntilAsync(
			() => s7Service.DisconnectCallCount == 2,
			cancellationToken: TestContext.Current.CancellationToken);

		s7Service.ReadManagingAreaCallCount.Should().Be(0,
			"reconnect reconciliation triggered by Connected-during-connect must gate on the version "
			+ "check and never read the managing area or apply a recipe on a version mismatch");
	}

	[Fact]
	public async Task RegisterReconnectApplyCallback_CalledTwice_Throws()
	{
		var (plc, _, _, _) = await BuildAsync();

		plc.RegisterReconnectApplyCallback(_ => Task.FromResult(Result.Ok()));

		var act = () => plc.RegisterReconnectApplyCallback(_ => Task.FromResult(Result.Ok()));

		act.Should().Throw<InvalidOperationException>(
			"a single coordinator owns the reconnect-apply pipeline; double registration indicates a wiring bug");
	}
}
