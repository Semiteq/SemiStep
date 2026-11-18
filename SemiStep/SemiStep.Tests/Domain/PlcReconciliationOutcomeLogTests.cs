using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Domain;

/// <summary>
/// A Warning outcome demoted to Information would be invisible on an installed machine, so the level
/// is asserted alongside the outcome name in every case.
/// </summary>
[Trait("Component", "Domain")]
[Trait("Area", "Reconnect")]
[Trait("Category", "Integration")]
public sealed class PlcReconciliationOutcomeLogTests : IDisposable
{
	private const int WaitActionId = 10;

	// The outcome line's template placeholders carry the result's property names.
	private const string OutcomeField = nameof(PlcReconciliationResult.Outcome);
	private const string LocalStepCountField = nameof(PlcReconciliationResult.LocalStepCount);
	private const string LocalValidField = nameof(PlcReconciliationResult.LocalValid);
	private const string PlcStepCountField = nameof(PlcReconciliationResult.PlcStepCount);
	private const string CommittedField = nameof(PlcReconciliationResult.Committed);
	private const string HasConflictSubscriberField = nameof(PlcReconciliationResult.HasConflictSubscriber);

	private readonly List<IDisposable> _disposables = [];

	public void Dispose()
	{
		foreach (var disposable in _disposables)
		{
			disposable.Dispose();
		}

		_disposables.Clear();
	}

	private async Task<(
		PlcLifecycleManager Plc,
		RecipeSession Session,
		StubS7Service S7Service,
		StubPlcSyncService SyncService,
		RecordingLogger<PlcLifecycleManager> Logger)> BuildAsync()
	{
		var configDir = TestConfigLocator.GetConfigDirectory("Standard");
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var s7Service = new StubS7Service();
		var syncService = new StubPlcSyncService();
		var logger = new RecordingLogger<PlcLifecycleManager>();

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
			.AddSingleton<ILogger<PlcLifecycleManager>>(logger)
			.BuildServiceProvider();

		var session = services.GetRequiredService<RecipeSession>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		session.Reset();

		_disposables.Add(plc);
		_disposables.Add(services);
		_disposables.Add(s7Service);

		return (plc, session, s7Service, syncService, logger);
	}

	private static Recipe BuildSingleStepRecipe()
	{
		return RecipeStubs.WithSteps(WaitActionId, 1);
	}

	private static async Task<RecordingLogger<PlcLifecycleManager>.LogEntry> WaitForOutcomeAsync(
		RecordingLogger<PlcLifecycleManager> logger,
		PlcReconciliationOutcome outcome)
	{
		await TestHelpers.WaitUntilAsync(
			() => OutcomeLines(logger).Any(e => outcome.Equals(e.Properties.GetValueOrDefault(OutcomeField))),
			cancellationToken: TestContext.Current.CancellationToken);

		var outcomeEntries = OutcomeLines(logger);
		outcomeEntries.Should().ContainSingle("one reconciliation must produce exactly one outcome line");

		return outcomeEntries[0];
	}

	private static IReadOnlyList<RecordingLogger<PlcLifecycleManager>.LogEntry> OutcomeLines(
		RecordingLogger<PlcLifecycleManager> logger)
	{
		var prefix = PlcLifecycleManager.ReconciliationOutcomePrefix;

		return [.. logger.Snapshot().Where(e => e.Message.StartsWith(prefix, StringComparison.Ordinal))];
	}

	private static void ShouldUseShortTemplate(RecordingLogger<PlcLifecycleManager>.LogEntry entry)
	{
		entry.Properties.Should().NotContainKey(CommittedField,
			"this exit unwound before the managing area was read, so the commit flag has no honest value");
		entry.Properties.Should().NotContainKey(HasConflictSubscriberField,
			"this exit never reached the conflict decision, so the subscriber field would be noise");
	}

	/// <summary>
	/// Drives one of the five exits that hand the local recipe to the sync executor while the session
	/// is edited inside the push.
	/// </summary>
	private async Task AssertLoggedRecipeIsThePushedOneAsync(PlcReconciliationOutcome outcome)
	{
		var (plc, session, s7Service, syncService, logger) = await BuildAsync();

		session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();
		ArrangePushExit(s7Service, outcome, session.Current);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		var pushIndex = syncService.NotifyRecipeChangedCallCount;

		// Stands in for the UI thread appending a step while the reconciliation is inside the push.
		syncService.OnNextNotifyRecipeChanged = () => session.AppendStep(WaitActionId);

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, outcome);

		syncService.NotifyRecipeChangedCalls[pushIndex].Recipe.Steps.Count.Should().Be(1,
			"the push captured the recipe as it stood before the edit");
		session.Current.Steps.Count.Should().Be(2, "the edit landed after the push");
		entry.Properties.Should().Contain(LocalStepCountField, 1,
			"the line must name the recipe handed to the sync executor, not the revision the edit left behind");
	}

	private static void ArrangePushExit(
		StubS7Service s7Service,
		PlcReconciliationOutcome outcome,
		Recipe localRecipe)
	{
		switch (outcome)
		{
			case PlcReconciliationOutcome.ManagingAreaReadFailed:
				// ManagingAreaToReturn left null: the stub answers the read with a failure.
				break;
			case PlcReconciliationOutcome.NotCommitted:
				s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: false, RecipeLines: 0);
				break;
			case PlcReconciliationOutcome.RecipeReadFailed:
				// RecipeToReturn left null: the stub answers the recipe read with a failure.
				s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
				break;
			case PlcReconciliationOutcome.PlcEmpty:
				s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 0);
				s7Service.RecipeToReturn = Recipe.Empty;
				break;
			case PlcReconciliationOutcome.Identical:
				s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
				s7Service.RecipeToReturn = RecipeStubs.DeepCopy(localRecipe);
				break;
			default:
				throw new ArgumentOutOfRangeException(
					nameof(outcome),
					outcome,
					"Only the five exits that push the local recipe are arranged here");
		}
	}

	[Fact]
	public async Task Reconciliation_WhenProtocolVersionMismatches_LogsProtocolVersionMismatchAtWarning()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.ProtocolVersionToReturn = Result.Ok(2);
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.ProtocolVersionMismatch);

		entry.Level.Should().Be(LogLevel.Warning,
			"a version mismatch aborts the reconciliation and must survive the field's Warning floor");
		entry.Properties.Should().Contain(LocalStepCountField, 0);
		ShouldUseShortTemplate(entry);
	}

	[Fact]
	public async Task Reconciliation_WhenProtocolVersionReadFails_LogsProtocolVersionCheckFailedAtWarning()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.ProtocolVersionToReturn = Result.Fail<int>("version read failed");
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.ProtocolVersionCheckFailed);

		entry.Level.Should().Be(LogLevel.Warning,
			"a failed version read sends triage elsewhere than a mismatch and must be named apart from it");
		ShouldUseShortTemplate(entry);
	}

	[Fact]
	public async Task Reconciliation_WhenManagingAreaReadFails_LogsManagingAreaReadFailedAndPushesLocal()
	{
		var (plc, _, s7Service, syncService, logger) = await BuildAsync();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		var pushesBefore = syncService.NotifyRecipeChangedCallCount;

		// ManagingAreaToReturn left null: the stub answers the read with a failure.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.ManagingAreaReadFailed);

		entry.Level.Should().Be(LogLevel.Warning,
			"a read failure that still pushes the local recipe over the PLC is a suspicious outcome");
		ShouldUseShortTemplate(entry);
		syncService.NotifyRecipeChangedCallCount.Should().Be(pushesBefore + 1,
			"this exit hands the local recipe to the sync executor, which this slice must not change");
	}

	[Fact]
	public async Task Reconciliation_WhenNotCommitted_LogsNotCommittedAtWarning()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: false, RecipeLines: 0);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.NotCommitted);

		entry.Level.Should().Be(LogLevel.Warning,
			"the uncommitted exit overwrites the PLC without asking and logged nothing at all before this slice");
		entry.Properties.Should().Contain(CommittedField, false,
			"the commit flag is established on this exit and belongs in the line");
	}

	[Fact]
	public async Task Reconciliation_WhenRecipeReadFails_LogsRecipeReadFailedAndPushesLocal()
	{
		var (plc, _, s7Service, syncService, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		var pushesBefore = syncService.NotifyRecipeChangedCallCount;

		// RecipeToReturn left null: the stub answers the recipe read with a failure.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.RecipeReadFailed);

		entry.Level.Should().Be(LogLevel.Warning);
		entry.Properties.Should().Contain(CommittedField, true,
			"the managing area was read on this exit even though the recipe was not");
		syncService.NotifyRecipeChangedCallCount.Should().Be(pushesBefore + 1,
			"this exit hands the local recipe to the sync executor, which this slice must not change");
	}

	[Fact]
	public async Task Reconciliation_WhenLocalEmptyAndApplySucceeds_LogsAppliedFromPlcAtInformation()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = BuildSingleStepRecipe();
		plc.RegisterReconnectApplyCallback(_ => Task.FromResult(Result.Ok()));

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.AppliedFromPlc);

		entry.Level.Should().Be(LogLevel.Information);

		// Asymmetric counts, so a swap of the two arguments fails one of the two checks.
		entry.Properties.Should().Contain(LocalStepCountField, 0);
		entry.Properties.Should().Contain(PlcStepCountField, 1,
			"the two step counts must name the side they came from");
	}

	[Fact]
	public async Task Reconciliation_WhenApplyCallbackMissing_LogsApplyFromPlcFailedAtWarning()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = BuildSingleStepRecipe();

		// No reconnect apply callback registered: the PLC recipe is discarded.
		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.ApplyFromPlcFailed);

		entry.Level.Should().Be(LogLevel.Warning,
			"a discarded PLC recipe must not read as a successful adoption");
	}

	[Fact]
	public async Task Reconciliation_WhenApplyCallbackThrows_LogsApplyFromPlcFailedAtWarning()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = BuildSingleStepRecipe();
		plc.RegisterReconnectApplyCallback(_ => throw new InvalidOperationException("apply blew up"));

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.ApplyFromPlcFailed);

		entry.Level.Should().Be(LogLevel.Warning,
			"a callback that throws leaves the PLC recipe discarded, which must not read as an adoption");
	}

	[Fact]
	public async Task Reconciliation_WhenApplyCallbackFails_LogsApplyFromPlcFailedAtWarning()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = BuildSingleStepRecipe();
		plc.RegisterReconnectApplyCallback(
			_ => Task.FromResult(Result.Fail("recipe analysis rejected the PLC recipe")));

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.ApplyFromPlcFailed);

		entry.Level.Should().Be(LogLevel.Warning,
			"a rejected apply leaves the PLC recipe discarded, which must not read as an adoption");
	}

	[Fact]
	public async Task Reconciliation_WhenRecipesDiffer_LogsConflictRaisedAtInformation()
	{
		var (plc, session, s7Service, _, logger) = await BuildAsync();

		session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();
		session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = BuildSingleStepRecipe();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		plc.PlcRecipeConflictDetected += (_, _) => { };

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.ConflictRaised);

		entry.Level.Should().Be(LogLevel.Information);
		entry.Properties.Should().Contain(HasConflictSubscriberField, true,
			"a raised conflict with a live subscriber and no dialog on screen points at the UI hop, not at Core");

		// Asymmetric counts, so a swap of the two arguments fails one of the two checks.
		entry.Properties.Should().Contain(LocalStepCountField, 2);
		entry.Properties.Should().Contain(PlcStepCountField, 1,
			"the two step counts must name the side they came from");
	}

	[Fact]
	public async Task Reconciliation_WhenPlcRecipeEmpty_LogsPlcEmptyAndPushesLocal()
	{
		var (plc, _, s7Service, syncService, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 0);
		s7Service.RecipeToReturn = Recipe.Empty;

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		var pushesBefore = syncService.NotifyRecipeChangedCallCount;

		// No conflict handler is attached here, which is the one case that reads the subscriber field
		// as False.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.PlcEmpty);

		entry.Level.Should().Be(LogLevel.Information);
		entry.Properties.Should().Contain(HasConflictSubscriberField, false,
			"the subscriber field separates 'Core never raised the conflict' from 'the UI hop dropped it'");
		syncService.NotifyRecipeChangedCallCount.Should().Be(pushesBefore + 1,
			"this exit hands the local recipe to the sync executor, which this slice must not change");
	}

	[Fact]
	public async Task Reconciliation_WhenRecipesIdentical_LogsIdenticalAtInformation()
	{
		var (plc, session, s7Service, _, logger) = await BuildAsync();

		session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();

		// A non-empty recipe on both sides: PlcEmpty takes precedence when both are empty, so
		// Identical can only be observed with steps present.
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = RecipeStubs.DeepCopy(session.Current);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.Identical);

		entry.Level.Should().Be(LogLevel.Information);
		entry.Properties.Should().Contain(LocalStepCountField, 1);
		entry.Properties.Should().Contain(PlcStepCountField, 1);
	}

	[Fact]
	public async Task Reconciliation_WhenReadTimesOutWithLiveToken_LogsTimedOutAtWarningWithException()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		// An S7.Net socket read timeout surfaces as a TaskCanceledException while the lifetime token
		// stays live.
		s7Service.ProtocolVersionReadShouldThrowCanceled = true;
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.TimedOut);

		entry.Level.Should().Be(LogLevel.Warning,
			"a read timeout is a fault, not a shutdown, and must reach the field log");
		entry.Exception.Should().BeOfType<TaskCanceledException>(
			"the short template renders identically for every read, so only the exception names the read that hung");
		ShouldUseShortTemplate(entry);
	}

	[Fact]
	public async Task Reconciliation_WhenLifetimeCancelledMidRead_LogsCancelledAtInformation()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = BuildSingleStepRecipe();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		var managingAreaGate = new ReadGate();
		s7Service.ManagingAreaReadGate = managingAreaGate;

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await managingAreaGate.Entered.WaitAsync(TestContext.Current.CancellationToken);

		plc.Dispose();
		managingAreaGate.Release();

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.Cancelled);

		entry.Level.Should().Be(LogLevel.Information,
			"a shutdown cancel is below the pinned source's floor at Debug, which would leave a begin line "
			+ "with no outcome line and read as a hang");
		ShouldUseShortTemplate(entry);
	}

	[Fact]
	public async Task Reconciliation_OnConnectedEdge_LogsBeginLineAtInformation()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: false, RecipeLines: 0);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.NotCommitted);

		IReadOnlyList<RecordingLogger<PlcLifecycleManager>.LogEntry> beginEntries =
			[.. logger.Snapshot().Where(e => e.Message == PlcLifecycleManager.ReconciliationStartedMessage)];

		beginEntries.Should().ContainSingle(
			"a started-but-never-finished run must be visible as a begin line with no outcome line");
		beginEntries[0].Level.Should().Be(LogLevel.Information);
	}

	[Fact]
	public async Task Reconciliation_OnOneConnectedEdge_LogsExactlyOneOutcomeLine()
	{
		var (plc, _, s7Service, _, logger) = await BuildAsync();

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: false, RecipeLines: 0);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		// Gate the managing-area read so the reconciliation genuinely finishes on a thread-pool thread
		// after the trigger returns; an ungated stub answers on the caller's thread and the run is over
		// before the count is taken.
		var managingAreaGate = new ReadGate();
		s7Service.ManagingAreaReadGate = managingAreaGate;

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await managingAreaGate.Entered.WaitAsync(TestContext.Current.CancellationToken);
		managingAreaGate.Release();

		await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.NotCommitted);

		// Settle window: a second outcome line, if one were emitted, lands within microseconds of
		// the first. Counting after a bounded delay makes "exactly one" an assertion, not a race.
		var settleWindow = TimeSpan.FromMilliseconds(250);
		await Task.Delay(settleWindow, TestContext.Current.CancellationToken);

		OutcomeLines(logger).Should().ContainSingle(
			"one reconciliation must produce exactly one outcome line, so the field log pairs begin with outcome");
	}

	[Fact]
	public async Task Reconciliation_WhenSessionChangesDuringManagingAreaReadFailedPush_LogsPushedRecipe()
	{
		await AssertLoggedRecipeIsThePushedOneAsync(PlcReconciliationOutcome.ManagingAreaReadFailed);
	}

	[Fact]
	public async Task Reconciliation_WhenSessionChangesDuringNotCommittedPush_LogsPushedRecipe()
	{
		await AssertLoggedRecipeIsThePushedOneAsync(PlcReconciliationOutcome.NotCommitted);
	}

	[Fact]
	public async Task Reconciliation_WhenSessionChangesDuringRecipeReadFailedPush_LogsPushedRecipe()
	{
		await AssertLoggedRecipeIsThePushedOneAsync(PlcReconciliationOutcome.RecipeReadFailed);
	}

	[Fact]
	public async Task Reconciliation_WhenSessionChangesDuringPlcEmptyPush_LogsPushedRecipe()
	{
		await AssertLoggedRecipeIsThePushedOneAsync(PlcReconciliationOutcome.PlcEmpty);
	}

	[Fact]
	public async Task Reconciliation_WhenSessionChangesDuringIdenticalPush_LogsPushedRecipe()
	{
		await AssertLoggedRecipeIsThePushedOneAsync(PlcReconciliationOutcome.Identical);
	}

	[Fact]
	public async Task Reconciliation_WhenLocalRecipeInvalid_LogsOutcomeWithLocalValidFalse()
	{
		var (plc, session, s7Service, _, logger) = await BuildAsync();

		new RecipeTestDriver(session).AddFor(3).AddWait(1f);
		session.IsValid.Should().BeFalse("the recipe has an unclosed For loop");

		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: false, RecipeLines: 0);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		var entry = await WaitForOutcomeAsync(logger, PlcReconciliationOutcome.NotCommitted);

		entry.Properties.Should().Contain(LocalValidField, false,
			"an invalid local recipe means the push outcome wrote nothing, which the line must say");
		entry.Level.Should().Be(LogLevel.Warning);
	}
}
