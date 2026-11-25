using AwesomeAssertions;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Domain;

[Trait("Component", "Domain")]
[Trait("Area", "Reconnect")]
[Trait("Category", "Integration")]
public sealed class PlcLifecycleManagerReconnectTests
{
	private const int WaitActionId = 10;
	private const int RieEtchActionId = 300;
	private const string CommentKey = "comment";

	private static async Task<(
		PlcLifecycleManager Plc,
		RecipeSession Session,
		StubS7Service S7Service,
		StubPlcSyncService SyncService,
		RecipeConverter Converter,
		RecordingLogger<PlcLifecycleManager> Logger)> BuildAsync(string? configDirectory = null)
	{
		var configDir = configDirectory ?? TestConfigLocator.GetConfigDirectory();
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var s7Service = new StubS7Service();
		var syncService = new StubPlcSyncService();
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
			.AddSingleton<IPlcSyncOwnership, StubPlcSyncOwnership>()
			.BuildServiceProvider();

		var session = services.GetRequiredService<RecipeSession>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		session.Reset();

		var converter = new RecipeConverter(services.GetRequiredService<RecipeMetadataRegistry>());

		return (plc, session, s7Service, syncService, converter, logger);
	}

	private static Recipe BuildSingleStepRecipe()
	{
		return RecipeStubs.WithSteps(WaitActionId, 1);
	}

	private static Recipe BuildWaitRecipeWithoutCommentKey()
	{
		return Recipe.Empty.AppendStep(
			new Step(WaitActionId, []).WithProperty(RecipeTestDriver.StepDurationColumn, PropertyValue.FromFloat(10f)));
	}

	private static Recipe RoundTripThroughPlcData(RecipeConverter converter, Recipe recipe)
	{
		return converter.ToRecipe(converter.FromRecipe(recipe).Value).Value;
	}

	private static async Task<bool> ReconcileAndReportConflictAsync(
		PlcLifecycleManager plc,
		StubS7Service s7Service,
		StubPlcSyncService syncService,
		Recipe plcRecipe)
	{
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: plcRecipe.StepCount);
		s7Service.RecipeToReturn = plcRecipe;

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		var conflictFired = false;
		plc.PlcRecipeConflictDetected += (_, _) => conflictFired = true;
		var countBeforeStateChange = syncService.NotifyRecipeChangedCallCount;

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await TestHelpers.WaitUntilAsync(
			() => conflictFired || syncService.NotifyRecipeChangedCallCount > countBeforeStateChange,
			cancellationToken: TestContext.Current.CancellationToken);

		return conflictFired;
	}

	private static async Task AssertIdenticalOutcomeLoggedAsync(RecordingLogger<PlcLifecycleManager> logger)
	{
		var prefix = PlcLifecycleManager.ReconciliationOutcomePrefix;
		var outcomeField = nameof(PlcReconciliationResult.Outcome);

		await TestHelpers.WaitUntilAsync(
			() => logger.Snapshot().Any(entry => entry.Message.StartsWith(prefix, StringComparison.Ordinal)),
			cancellationToken: TestContext.Current.CancellationToken);

		var outcomeLines = logger.Snapshot()
			.Where(entry => entry.Message.StartsWith(prefix, StringComparison.Ordinal))
			.ToList();
		outcomeLines.Should().ContainSingle()
			.Which.Properties[outcomeField].Should().Be(PlcReconciliationOutcome.Identical);
	}

	[Fact]
	public async Task StateChanged_Connected_WhenRecipesDiffer_FiresConflictDetected()
	{
		var (plc, session, s7Service, _, _, _) = await BuildAsync();

		var appendResult = session.AppendStep(WaitActionId);
		appendResult.IsSuccess.Should().BeTrue();

		var plcRecipe = BuildSingleStepRecipe();
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = plcRecipe;

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		Recipe? conflictLocalRecipe = null;
		Recipe? conflictPlcRecipe = null;
		plc.PlcRecipeConflictDetected += (local, plcRec) =>
		{
			conflictLocalRecipe = local;
			conflictPlcRecipe = plcRec;
		};

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

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
		var (plc, session, s7Service, syncService, _, _) = await BuildAsync();

		var appendResult = session.AppendStep(WaitActionId);
		appendResult.IsSuccess.Should().BeTrue();

		// Reusing BuildSingleStepRecipe() would yield empty Properties, which differ from the
		// session's populated step and would wrongly trip the conflict branch.
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = RecipeStubs.DeepCopy(session.Current);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		var conflictFired = false;
		plc.PlcRecipeConflictDetected += (_, _) => conflictFired = true;

		var countBeforeStateChange = syncService.NotifyRecipeChangedCallCount;

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await TestHelpers.WaitUntilAsync(
			() => syncService.NotifyRecipeChangedCallCount > countBeforeStateChange,
			cancellationToken: TestContext.Current.CancellationToken);

		conflictFired.Should().BeFalse(
			"identical PLC and PC recipe content must not raise PlcRecipeConflictDetected");
	}

	[Fact]
	public async Task StateChanged_Connected_WhenNotCommitted_PushesLocalRecipe()
	{
		var (plc, _, s7Service, syncService, _, _) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: false, RecipeLines: 0);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		var countBeforeStateChange = syncService.NotifyRecipeChangedCallCount;

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await TestHelpers.WaitUntilAsync(
			() => syncService.NotifyRecipeChangedCallCount > countBeforeStateChange,
			cancellationToken: TestContext.Current.CancellationToken);

		syncService.NotifyRecipeChangedCallCount.Should().BeGreaterThan(countBeforeStateChange,
			"when committed=false the manager must push the local recipe to the PLC via NotifyRecipeChanged");
	}

	[Fact]
	public async Task StateChanged_Disconnected_WhenSyncEnabled_CallsHandleConnectionLost()
	{
		var (plc, _, s7Service, syncService, _, _) = await BuildAsync();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Disconnected);

		syncService.WasHandleConnectionLostCalled.Should().BeTrue(
			"IPlcSyncService.HandleConnectionLost() must be called when the PLC disconnects while sync is enabled");
		syncService.WasResetForDisableCalled.Should().BeFalse(
			"a connection drop is a loss alarm, not a clean teardown, so it must not take the disable path");
	}

	[Fact]
	public async Task DisableSync_CallsResetForDisableOnSyncService()
	{
		var (plc, _, _, syncService, _, _) = await BuildAsync();

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
		var (plc, _, s7Service, _, _, _) = await BuildAsync();

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
		var (plc, session, s7Service, _, _, _) = await BuildAsync();

		var plcRecipe = BuildSingleStepRecipe();
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = plcRecipe;

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
		var (plc, _, s7Service, syncService, _, _) = await BuildAsync();
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
		var (plc, _, s7Service, syncService, _, _) = await BuildAsync();
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
		var (plc, _, s7Service, syncService, _, _) = await BuildAsync();
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
		var (plc, _, s7Service, _, _, _) = await BuildAsync();

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
	public async Task StateChanged_Connected_WhenLifetimeCancelledMidRead_AbortsAtReadWithoutReadingRecipe()
	{
		var (plc, _, s7Service, _, _, _) = await BuildAsync();

		// The broken (token-ignored) path would proceed past a committed managing area to the recipe read
		// and the apply callback. Wire both so the divergence is observable: committed area + non-empty
		// recipe + empty local session (session.Reset in BuildAsync) is the "adopt PLC recipe" shape.
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = BuildSingleStepRecipe();

		var applyCallbackInvoked = false;
		plc.RegisterReconnectApplyCallback(_ =>
		{
			applyCallbackInvoked = true;
			return Task.FromResult(Result.Ok());
		});

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		// Gate the managing-area read (the first read after the version check).
		var managingAreaGate = new ReadGate();
		s7Service.ManagingAreaReadGate = managingAreaGate;

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await managingAreaGate.Entered.WaitAsync(TestContext.Current.CancellationToken);

		// Cancel the lifetime token mid-wait, then release the gate. A genuine cancel makes the gated
		// read throw before it returns, so reconciliation never advances to the recipe read.
		plc.Dispose();
		managingAreaGate.Release();

		// The discriminator: the downstream recipe read must NOT happen. A silently-ignored token would
		// let the released managing-area read return and reconciliation would proceed to the recipe read
		// (count 1); the working plumbing aborts at the read (count 0). Bounded wait: if the recipe read
		// is ever reached it signals within microseconds; timing out means it was never reached.
		var settleWindow = TimeSpan.FromMilliseconds(250);
		var recipeReadReached = await Task.WhenAny(
			s7Service.RecipeReadReached,
			Task.Delay(settleWindow, TestContext.Current.CancellationToken))
			== s7Service.RecipeReadReached;

		recipeReadReached.Should().BeFalse(
			"a cancelled lifetime token must abort reconciliation at the in-flight managing-area read, "
			+ "never advancing to the downstream recipe read");
		s7Service.ReadRecipeFromPlcCallCount.Should().Be(0,
			"the recipe read is downstream of the managing-area read; a cancel at the read leaves it uncalled");
		applyCallbackInvoked.Should().BeFalse(
			"aborting at the managing-area read must not reach the apply callback");
	}

	[Fact]
	public async Task RegisterReconnectApplyCallback_CalledTwice_Throws()
	{
		var (plc, _, _, _, _, _) = await BuildAsync();

		plc.RegisterReconnectApplyCallback(_ => Task.FromResult(Result.Ok()));

		var act = () => plc.RegisterReconnectApplyCallback(_ => Task.FromResult(Result.Ok()));

		act.Should().Throw<InvalidOperationException>(
			"a single coordinator owns the reconnect-apply pipeline; double registration indicates a wiring bug");
	}

	[Fact]
	public async Task StateChanged_Connected_WhenCsvShapedStepMatchesPlcData_DoesNotFireConflict()
	{
		var (plc, session, s7Service, syncService, converter, logger) = await BuildAsync();
		var localRecipe = BuildWaitRecipeWithoutCommentKey();
		session.LoadAsCurrent(localRecipe).IsSuccess.Should().BeTrue();

		var plcRecipe = RoundTripThroughPlcData(converter, localRecipe);

		var conflictFired = await ReconcileAndReportConflictAsync(plc, s7Service, syncService, plcRecipe);

		conflictFired.Should().BeFalse(
			"a blank comment cell and the empty string the PLC holds are the same PLC data");
		await AssertIdenticalOutcomeLoggedAsync(logger);
	}

	[Fact]
	public async Task StateChanged_Connected_WhenOneValueDiffersInPlcData_FiresConflict()
	{
		var (plc, session, s7Service, syncService, converter, _) = await BuildAsync();
		var localRecipe = BuildWaitRecipeWithoutCommentKey();
		session.LoadAsCurrent(localRecipe).IsSuccess.Should().BeTrue();

		var roundTrip = RoundTripThroughPlcData(converter, localRecipe);
		var plcRecipe = Recipe.Empty.AppendStep(
			roundTrip.Steps[0].WithProperty(RecipeTestDriver.StepDurationColumn, PropertyValue.FromFloat(11f)));

		var conflictFired = await ReconcileAndReportConflictAsync(plc, s7Service, syncService, plcRecipe);

		conflictFired.Should().BeTrue("a step duration that differs in the PLC is a different recipe");
	}

	[Fact]
	public async Task StateChanged_Connected_WhenRieEtchStepOmitsInactiveColumns_DoesNotFireConflict()
	{
		var rieConfigDirectory = ShippedConfigLocator.GetConfigDirectory("RIE");
		var (plc, session, s7Service, syncService, converter, logger) = await BuildAsync(rieConfigDirectory);
		session.AppendStep(RieEtchActionId).IsSuccess.Should().BeTrue();

		var plcRecipe = RoundTripThroughPlcData(converter, session.Current);

		var conflictFired = await ReconcileAndReportConflictAsync(plc, s7Service, syncService, plcRecipe);

		conflictFired.Should().BeFalse(
			"inactive capacitor columns are written as zero, which is what the PLC holds");
		await AssertIdenticalOutcomeLoggedAsync(logger);
	}

	[Fact]
	public async Task StateChanged_Connected_WhenLocalValueHasWrongType_FiresConflict()
	{
		var (plc, session, s7Service, syncService, converter, logger) = await BuildAsync();
		var localRecipe = Recipe.Empty.AppendStep(
			new Step(WaitActionId, [])
				.WithProperty(RecipeTestDriver.StepDurationColumn, PropertyValue.FromFloat(10f))
				.WithProperty(CommentKey, PropertyValue.FromInt(5)));
		session.LoadAsCurrent(localRecipe).IsSuccess.Should().BeTrue();
		var plcRecipe = RoundTripThroughPlcData(converter, BuildWaitRecipeWithoutCommentKey());

		var conflictFired = await ReconcileAndReportConflictAsync(plc, s7Service, syncService, plcRecipe);

		conflictFired.Should().BeTrue("a local recipe the PC cannot encode is not proven equal to the PLC");
		logger.Snapshot()
			.Where(entry => entry.Level == LogLevel.Warning && entry.Properties.ContainsKey("StepNumber"))
			.Should().ContainSingle()
			.Which.Properties["StepNumber"].Should().Be(1);
	}

	[Fact]
	public async Task StateChanged_Connected_WhenPlcHoldsValueInKeyLocalOmits_FiresConflict()
	{
		var (plc, session, s7Service, syncService, converter, _) = await BuildAsync();
		var localRecipe = BuildWaitRecipeWithoutCommentKey();
		session.LoadAsCurrent(localRecipe).IsSuccess.Should().BeTrue();

		var roundTrip = RoundTripThroughPlcData(converter, localRecipe);
		var plcRecipe = Recipe.Empty.AppendStep(
			roundTrip.Steps[0].WithProperty(CommentKey, PropertyValue.FromString("x")));

		var conflictFired = await ReconcileAndReportConflictAsync(plc, s7Service, syncService, plcRecipe);

		conflictFired.Should().BeTrue("a missing local key matches only the type default, not a value the PLC holds");
	}
}
