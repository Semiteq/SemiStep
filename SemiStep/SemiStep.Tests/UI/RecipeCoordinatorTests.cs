using System.Collections.Immutable;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "Coordinator")]
[Trait("Category", "Integration")]
public sealed class RecipeCoordinatorTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();

	public ValueTask InitializeAsync()
	{
		return _fixture.InitializeAsync();
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void AppendStep_EmitsStepAppendedSignal()
	{
		_fixture.Session.Reset();
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepAppended>()
			.Which.Index.Should().Be(0);
	}

	[AvaloniaFact]
	public void AppendStep_ReturnsSuggestedSelection_AsLastIndex()
	{
		_fixture.Session.Reset();

		var result = _fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		result.Value.Should().Be(0);
	}

	[AvaloniaFact]
	public void InsertStep_EmitsStepsInsertedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepsInserted>()
			.Which.Should().BeEquivalentTo(new MutationSignal.StepsInserted(0, 1));
	}

	[AvaloniaFact]
	public void InsertStep_ReturnsSuggestedSelection_AsInsertedIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);

		result.Value.Should().Be(0);
	}

	[AvaloniaFact]
	public void RemoveStep_EmitsStepRemovedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.RemoveStep(0);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepRemoved>()
			.Which.RemovedIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void RemoveStep_ReturnsNullSelection_WhenRecipeBecomesEmpty()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.RemoveStep(0);

		result.Value.Should().BeNull();
	}

	[AvaloniaFact]
	public void RemoveStep_ReturnsClampedSelection_WhenRemovingLast()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.RemoveStep(2);

		result.Value.Should().Be(1);
	}

	[AvaloniaFact]
	public void RemoveSteps_EmitsStepsRemovedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.RemoveSteps(new[] { 0, 1 });

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepsRemoved>();
	}

	[AvaloniaFact]
	public void ChangeStepAction_EmitsStepActionChangedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepActionChanged>()
			.Which.StepIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void UpdateStepProperty_EmitsPropertyUpdatedSignal_WithCorrectStepIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "5");

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.PropertyUpdated>()
			.Which.StepIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void Undo_EmitsRecipeReplacedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.Undo();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[AvaloniaFact]
	public void Redo_EmitsRecipeReplacedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.Undo();
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.Redo();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[AvaloniaFact]
	public void NewRecipe_EmitsRecipeReplacedSignal()
	{
		_fixture.Session.Reset();
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.NewRecipe();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[AvaloniaFact]
	public void NewRecipe_RebuildsPanelFromFreshRecipeReasons()
	{
		_fixture.Session.Reset();
		// Establish a dirty panel precondition via a genuine structural warning: an unclosed
		// For loop is a real snapshot reason, so the snapshot-fed panel becomes non-empty.
		_fixture.Coordinator.AppendStep(RecipeTestDriver.ForLoopActionId);
		_fixture.MessagePanel.Entries.Should().Contain(
			entry => entry.IsWarning,
			"an unclosed For loop is a genuine structural defect that lands in the snapshot");

		_fixture.Coordinator.NewRecipe();

		_fixture.MessagePanel.Entries.Should().BeEmpty(
			"NewRecipe rebuilds the panel from the fresh recipe's snapshot, clearing the prior warning");
	}

	[AvaloniaFact]
	public void AppendStep_Failure_DoesNotEmitSignal()
	{
		_fixture.Session.Reset();
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Mutated += sink.OnMutation;
		var signals = sink.Signals;

		_fixture.Coordinator.AppendStep(9999);

		signals.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void AppendStep_Failure_DoesNotPopulateMessagePanel()
	{
		_fixture.Session.Reset();

		var result = _fixture.Coordinator.AppendStep(9999);

		result.IsFailed.Should().BeTrue("appending with an unknown action id must be rejected");
		_fixture.MessagePanel.ErrorCount.Should().Be(0,
			"a rejected append is an operation outcome, not a structural defect, so it must not enter the panel");
		_fixture.MessagePanel.Entries.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void IsDirty_True_AfterMutation()
	{
		_fixture.Session.Reset();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.IsDirty.Should().BeTrue();
	}

	[AvaloniaFact]
	public void CanUndo_True_AfterMutation()
	{
		_fixture.Session.Reset();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.CanUndo.Should().BeTrue();
	}

	[AvaloniaFact]
	public void CanRedo_True_AfterUndoingMutation()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.Undo();

		_fixture.Coordinator.CanRedo.Should().BeTrue();
	}

	[AvaloniaFact]
	public void AppendStep_Failure_ReturnsFailed()
	{
		_fixture.Session.Reset();

		var result = _fixture.Coordinator.AppendStep(9999);

		result.IsFailed.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ChangeStepAction_Failure_ReturnsFailed()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.ChangeStepAction(0, 9999);

		result.IsFailed.Should().BeTrue();
	}

	[AvaloniaFact]
	public void UpdateStepProperty_Failure_ReturnsFailed()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.UpdateStepProperty(0, "NonExistentColumn", "value");

		result.IsFailed.Should().BeTrue();
	}

	[AvaloniaFact]
	public void InsertSteps_ReturnsSuggestedSelection_AsStartIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.InsertSteps(0, new[] { _fixture.Coordinator.CurrentRecipe.Steps[0] });

		result.Value.Should().Be(0);
	}

	[AvaloniaFact]
	public void ChangeStepAction_ReturnsSuggestedSelection_AsStepIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.ChangeStepAction(1, RecipeTestDriver.ForLoopActionId);

		result.Value.Should().Be(1);
	}

	[AvaloniaFact]
	public void RemoveSteps_ReturnsClampedSelection_AsMinIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.RemoveSteps(new[] { 1, 2 });

		result.Value.Should().Be(0);
	}

	[AvaloniaFact]
	public void RemoveSteps_ReturnsNull_WhenRecipeBecomesEmpty()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.RemoveSteps(new[] { 0, 1 });

		result.Value.Should().BeNull();
	}

	[AvaloniaFact]
	public void PlcStateChange_Failure_DoesNotAddEntriesToMessagePanel()
	{
		_fixture.Session.Reset();
		var entryCountBeforePlcChange = _fixture.MessagePanel.Entries.Count;

		_fixture.PlcSyncService.PushPlcState(
			Result.Fail<PlcSessionSnapshot>("PLC connection lost"));

		_fixture.MessagePanel.Entries.Count.Should().Be(entryCountBeforePlcChange);
		_fixture.MessagePanel.Entries.Should().NotContain(e => e.Message == "PLC connection lost");
	}

	[AvaloniaFact]
	public void UpdateStepProperty_RejectedInvalidValue_DoesNotPopulateMessagePanel()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.MessagePanel.Entries.Should().BeEmpty("a valid single-step recipe has no structural defects");

		var result = _fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "999999");

		result.IsFailed.Should().BeTrue("the value exceeds the property maximum and the edit is rejected");
		_fixture.MessagePanel.Entries.Should().BeEmpty(
			"a rejected edit is an operation outcome, not a structural defect, so it must not enter the panel");
		_fixture.MessagePanel.ErrorCount.Should().Be(0);
	}

	[AvaloniaFact]
	public void SuccessfulMutation_StructuralWarning_SurfacesInPanelAndSelfHeals()
	{
		_fixture.Session.Reset();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.ForLoopActionId);

		_fixture.MessagePanel.Entries.Should().Contain(
			e => e.IsWarning && e.Message.Contains("Unclosed For loop", StringComparison.OrdinalIgnoreCase),
			"a successful mutation that leaves the recipe structurally defective surfaces the warning from the snapshot");

		_fixture.Coordinator.AppendStep(RecipeTestDriver.EndForLoopActionId);

		_fixture.MessagePanel.Entries.Should().BeEmpty(
			"closing the For loop restores structural validity and the panel self-heals from the snapshot");
	}

	[AvaloniaFact]
	public async Task SaveRecipeAsync_Success_RaisesMutatedEvent()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var mutatedCount = 0;
		_fixture.Coordinator.Mutated += _ => mutatedCount++;
		var tempFilePath = Path.Combine(Path.GetTempPath(), $"SemiStep.MutatedTest.{Guid.NewGuid():N}.csv");

		try
		{
			var result = await _fixture.Coordinator.SaveRecipeAsync(tempFilePath);

			result.IsSuccess.Should().BeTrue();
			mutatedCount.Should().BeGreaterThan(0,
				"SaveRecipeAsync must raise Mutated after MarkSaved so the window title and IsDirty refresh");
			_fixture.Coordinator.IsDirty.Should().BeFalse();
		}
		finally
		{
			if (File.Exists(tempFilePath))
			{
				File.Delete(tempFilePath);
			}
		}
	}

	[AvaloniaFact]
	public async Task ReconnectApply_Failure_SurfacesTransientErrorAndLeavesPanelClean()
	{
		_fixture.Session.Reset();
		_fixture.MessagePanel.Entries.Should().BeEmpty("an empty recipe has no structural defects");

		// The reconnect-apply path validates the PLC recipe via ImportedRecipeValidator and fails
		// when it carries an unknown action id; the failure has no initiating VM, so the coordinator
		// must surface it as an operation outcome in the message panel.
		var invalidPlcRecipe = new Recipe(ImmutableList.Create(
			new Step(9999, ImmutableDictionary<PropertyId, PropertyValue>.Empty)));
		_fixture.S7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		_fixture.S7Service.RecipeToReturn = invalidPlcRecipe;

		var enableResult = await _fixture.Coordinator.EnableSync();
		enableResult.IsSuccess.Should().BeTrue();

		// Simulate an auto-reconnect: with an empty local recipe and a committed non-empty PLC
		// recipe, the reconcile path routes the PLC recipe through ApplyReconnectPlcRecipeAsync.
		_fixture.S7Service.RaiseStateChanged(PlcConnectionState.Connected);

		await TestHelpers.WaitUntilAsync(
			() => _fixture.MessagePanel.Entries.Count > 0,
			cancellationToken: TestContext.Current.CancellationToken);

		var operationEntry = _fixture.MessagePanel.Entries[0];
		operationEntry.Severity.Should().Be(MessageSeverity.Error,
			"a failed reconnect-apply has no initiating VM, so the coordinator must report it as an operation error");
		operationEntry.Message.Should().Contain("PLC reconnect",
			"the operation message must identify the reconnect-apply origin");

		_fixture.MessagePanel.ErrorCount.Should().Be(0,
			"a failed reconnect-apply is an operation outcome, not a structural defect, "
			+ "so it must not inflate the validation count");
	}

	[AvaloniaFact]
	public void SuccessfulMutation_ClearsPriorOperationMessage()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.MessagePanel.ReportError("stale operation error");
		_fixture.MessagePanel.Entries.Should().ContainSingle(
			"the operation error is the only row before the next mutation");

		var result = _fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		result.IsSuccess.Should().BeTrue();
		_fixture.MessagePanel.Entries.Should().BeEmpty(
			"a successful mutation clears the stale operation message via RaiseMutatedSafely");
	}

	[AvaloniaFact]
	public void RejectedMutation_KeepsPriorOperationError()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.MessagePanel.ReportError("operation error");

		var result = _fixture.Coordinator.UpdateStepProperty(99, "duration", "1");

		result.IsFailed.Should().BeTrue(
			"updating a property on a nonexistent step is rejected before DispatchMutation");
		_fixture.MessagePanel.Entries.Should().ContainSingle(
			"a rejected mutation never reaches RaiseMutatedSafely, so the operation error survives");
		_fixture.MessagePanel.Entries[0].Message.Should().Be("operation error");
	}
}
