using System.Collections.Immutable;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedRecipeGridSurfaceTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private readonly RecordingLogger<TransposedRecipeGridSurface> _logger = new();
	private TransposedRecipeGridSurface _surface = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_surface = _fixture.CreateTransposedSurface(_logger);
		_surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void NewRecipe_LeavesGridEmpty()
	{
		_fixture.Coordinator.NewRecipe();

		_surface.StepColumns.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void AppendStep_AddsColumn_WithOneCellPerParameter()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.StepColumns.Should().HaveCount(1);
		_surface.StepColumns[0].Row.StepNumber.Should().Be(1);
		_surface.StepColumns[0].Cells.Should().HaveCount(_surface.ParameterDescriptors.Count);
	}

	[AvaloniaFact]
	public void InsertStep_InsertsColumnAtIndex_AndRenumbersSubsequentColumns()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.PauseActionId);

		_surface.StepColumns[0].Row.ActionId.Should().Be(RecipeTestDriver.PauseActionId);
		_surface.StepColumns[1].Row.StepNumber.Should().Be(2);
		_surface.StepColumns[2].Row.StepNumber.Should().Be(3);
	}

	[AvaloniaFact]
	public void RemoveStep_RemovesColumn_AndRenumbersRemainingColumns()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveStep(0);

		_surface.StepColumns.Should().HaveCount(2);
		_surface.StepColumns[0].Row.StepNumber.Should().Be(1);
		_surface.StepColumns[1].Row.StepNumber.Should().Be(2);
	}

	[AvaloniaFact]
	public void RemoveSteps_RemovesMultipleColumns_AndRenumbers()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveSteps(new[] { 0, 2 });

		_surface.StepColumns.Should().HaveCount(1);
		_surface.StepColumns[0].Row.StepNumber.Should().Be(1);
	}

	[AvaloniaFact]
	public void ChangeStepAction_RebuildsColumn_WithNewActionId()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var columnBefore = _surface.StepColumns[0];

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		_surface.StepColumns[0].Should().NotBeSameAs(columnBefore);
		_surface.StepColumns[0].Row.ActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[AvaloniaFact]
	public void ChangeStepAction_MarksNewActionCellsChanged()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		var expected = _fixture.Coordinator.CurrentRecipe.Steps[0].Properties.Keys
			.Select(id => id.Value)
			.ToList();
		_surface.StepColumns[0].Row.ChangedColumns.Should().BeEquivalentTo(expected);
		GetCell(0, RecipeTestDriver.TaskColumn).IsChanged.Should().BeTrue();
	}

	[AvaloniaFact]
	public void Undo_TriggersFullRebuild_ColumnCountMatchesRecipe()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.Undo();
		_fixture.Coordinator.Undo();

		_surface.StepColumns.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void StateRefreshed_IsNoOpForProjection()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var columnBefore = _surface.StepColumns[0];

		_surface.OnMutation(new MutationSignal.StateRefreshed());

		_surface.StepColumns.Should().HaveCount(1);
		_surface.StepColumns[0].Should().BeSameAs(columnBefore);
	}

	[AvaloniaFact]
	public void PropertyUpdated_RefreshesAllCellsOfColumn_IncludingCoupledCell()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var durationCell = GetCell(0, RecipeTestDriver.StepDurationColumn);
		var commentCell = GetCell(0, RecipeTestDriver.CommentColumn);

		// Session-level write standing in for a formula recalc: a second property of the same step
		// changes inside the mutation window without its own signal. Until the PropertyUpdated for
		// the edited cell arrives, the projection must still show the stale value.
		_fixture.Session.UpdateStepProperty(0, RecipeTestDriver.CommentColumn, "coupled");
		commentCell.Value.Should().Be(string.Empty);

		var changedProperties = new List<string?>();
		commentCell.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "15");

		durationCell.Value.Should().Be(15f);
		commentCell.Value.Should().Be("coupled");
		changedProperties.Should().Contain(nameof(ParameterCellViewModel.Value));
	}

	[AvaloniaFact]
	public void RemoveStep_DropsStaleSelectionIndex_AndCanDeleteStepReflectsIt()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.UpdateSelection(new[] { 2 });
		bool? latestCanDelete = null;
		using var subscription = _surface.CanDeleteStep.Subscribe(value => latestCanDelete = value);
		latestCanDelete.Should().BeTrue();

		_fixture.Coordinator.RemoveStep(2);

		_surface.SelectedStepIndices.Should().BeEmpty();
		_surface.SelectedStepIndex.Should().Be(-1);
		latestCanDelete.Should().BeFalse();
	}

	[AvaloniaFact]
	public void RemoveStep_KeepsSelectionIndicesStillInRange()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.UpdateSelection(new[] { 0, 2 });

		_fixture.Coordinator.RemoveStep(2);

		_surface.SelectedStepIndices.Should().Equal(0);
	}

	[AvaloniaFact]
	public void SuccessfulCellEdit_ClearsChangedFlag()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);
		var taskCell = GetCell(0, RecipeTestDriver.TaskColumn);
		taskCell.IsChanged.Should().BeTrue();

		taskCell.Value = "5";

		taskCell.IsChanged.Should().BeFalse();
	}

	[AvaloniaFact]
	public void RejectedCellEdit_PreservesChangedFlag()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var durationCell = GetCell(0, RecipeTestDriver.StepDurationColumn);
		_surface.StepColumns[0].Row.MarkChanged(new[] { RecipeTestDriver.StepDurationColumn });

		// "999999" exceeds the duration maximum: the coordinator rejects the edit and the surface
		// takes the early return before ClearChanged, so the orange highlight stays.
		durationCell.Value = "999999";

		durationCell.IsChanged.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ExecutionStart_ClearsChangedHighlights_AndStampsCurrentAndPastColumns()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.StepColumns[0].Row.MarkChanged(new[] { RecipeTestDriver.StepDurationColumn });

		_fixture.S7Service.PushExecutionState(
			PlcExecutionInfo.Empty with { RecipeActive = true, ActualLine = 1 });

		GetCell(0, RecipeTestDriver.StepDurationColumn).IsChanged.Should().BeFalse();
		_surface.StepColumns[0].Row.IsPastStep.Should().BeTrue();
		_surface.StepColumns[0].Row.IsCurrentStep.Should().BeFalse();
		_surface.StepColumns[1].Row.IsCurrentStep.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ExecutionBackwardJump_ClearsStalePastFlags()
	{
		_fixture.Coordinator.NewRecipe();
		for (var i = 0; i < 10; i++)
		{
			_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		}

		_fixture.S7Service.PushExecutionState(
			PlcExecutionInfo.Empty with { RecipeActive = true, ActualLine = 7 });
		_fixture.S7Service.PushExecutionState(
			PlcExecutionInfo.Empty with { RecipeActive = true, ActualLine = 3 });

		for (var i = 0; i < 3; i++)
		{
			_surface.StepColumns[i].Row.IsPastStep.Should().BeTrue($"step {i} should be past");
			_surface.StepColumns[i].Row.IsCurrentStep.Should().BeFalse();
		}

		_surface.StepColumns[3].Row.IsCurrentStep.Should().BeTrue();
		_surface.StepColumns[3].Row.IsPastStep.Should().BeFalse();

		for (var i = 4; i <= 7; i++)
		{
			_surface.StepColumns[i].Row.IsPastStep.Should().BeFalse($"step {i} stale past flag must be cleared");
			_surface.StepColumns[i].Row.IsCurrentStep.Should().BeFalse();
		}
	}

	[AvaloniaFact]
	public void ExecutionStop_ClearsStepHighlights()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.S7Service.PushExecutionState(
			PlcExecutionInfo.Empty with { RecipeActive = true, ActualLine = 1 });

		_fixture.SetRecipeActive(false);

		_surface.StepColumns.Should().AllSatisfy(column =>
		{
			column.Row.IsCurrentStep.Should().BeFalse();
			column.Row.IsPastStep.Should().BeFalse();
		});
	}

	[AvaloniaFact]
	public void StepStartTimes_RefreshedAfterMutation()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.StepColumns[0].Row.StepStartTime.Should().NotBeNull();
		_surface.StepColumns[1].Row.StepStartTime.Should().NotBeNull();
	}

	[AvaloniaFact]
	public void LoopDepths_RefreshedAfterMutation()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.ForLoopActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.EndForLoopActionId);

		_surface.StepColumns.Should().AllSatisfy(column => column.Row.ForDepth.Should().Be(1));
	}

	[AvaloniaFact]
	public void OnMutation_PropertyUpdated_OutOfRange_LogsWarningAndDoesNotThrow()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(new MutationSignal.PropertyUpdated(99));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("PropertyUpdated"));
	}

	[AvaloniaFact]
	public void OnMutation_StepRemoved_OutOfRange_LogsWarningAndDoesNotThrow()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(new MutationSignal.StepRemoved(42));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepRemoved"));
	}

	[AvaloniaFact]
	public void OnMutation_StepsRemoved_AnyIndexOutOfRange_LogsWarningAndRemovesNothing()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(
			new MutationSignal.StepsRemoved(ImmutableArray.Create(0, 99)));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepsRemoved"));
		_surface.StepColumns.Should().HaveCount(
			2, "the removal is all-or-nothing: one bad index drops the whole signal");
		_surface.StepColumns[0].Row.StepNumber.Should().Be(1);
		_surface.StepColumns[1].Row.StepNumber.Should().Be(2);
	}

	[AvaloniaFact]
	public void OnMutation_PropertyUpdated_BeyondColumns_LogsWarningAndDoesNotThrow()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		// Simulate a projection that lags behind the recipe: index 1 is valid for the recipe
		// but beyond the surviving columns.
		_surface.StepColumns[1].Dispose();
		_surface.StepColumns.RemoveAt(1);

		var act = () => _surface.OnMutation(new MutationSignal.PropertyUpdated(1));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("PropertyUpdated"));
	}

	[AvaloniaFact]
	public void OnMutation_StepAppended_OutOfRange_LogsWarningAndAddsNothing()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(new MutationSignal.StepAppended(99));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepAppended"));
		_surface.StepColumns.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void OnMutation_StepsInserted_OutOfRecipeRange_LogsWarningAndInsertsNothing()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(new MutationSignal.StepsInserted(0, 99));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepsInserted"));
		_surface.StepColumns.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void OnMutation_StepsInserted_StartBeyondColumns_LogsWarningAndInsertsNothing()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		// startIndex 1 with count 1 is valid for the recipe (StepCount=2) but beyond the
		// surviving columns after the projection lost both.
		_surface.StepColumns[1].Dispose();
		_surface.StepColumns.RemoveAt(1);
		_surface.StepColumns[0].Dispose();
		_surface.StepColumns.RemoveAt(0);

		var act = () => _surface.OnMutation(new MutationSignal.StepsInserted(1, 1));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepsInserted"));
		_surface.StepColumns.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void OnMutation_StepActionChanged_OutOfRange_LogsWarningAndDoesNotThrow()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(new MutationSignal.StepActionChanged(99));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepActionChanged"));
	}

	[AvaloniaFact]
	public void RejectedCellEdit_ReportsFailureToMessagePanel()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		GetCell(0, RecipeTestDriver.StepDurationColumn).Value = "not_a_valid_number";

		var operationEntry = _fixture.MessagePanel.Entries.Should()
			.ContainSingle(entry => entry.IsError).Subject;
		operationEntry.Message.Should().StartWith("Step 1:");
	}

	[AvaloniaFact]
	public void ChangeToUnknownAction_ReportsErrorToMessagePanel()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.StepColumns[0].Row.SetPropertyValue("action", "999999");

		var operationEntry = _fixture.MessagePanel.Entries.Should()
			.ContainSingle(entry => entry.IsError).Subject;
		operationEntry.Message.Should().StartWith("Step 1: Failed to change action");
	}

	[AvaloniaFact]
	public void EditOnCanonicalSibling_ClearsChangedFlag_OnTransposedColumn()
	{
		var canonicalSurface = _fixture.CreateCanonicalSurface();
		canonicalSurface.Initialize();

		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		var transposedCell = GetCell(0, RecipeTestDriver.TaskColumn);
		transposedCell.IsChanged.Should().BeTrue("the action change marks seeded cells on both surfaces");

		canonicalSurface.RecipeRows[0].SetPropertyValue(RecipeTestDriver.TaskColumn, "5");

		transposedCell.IsChanged.Should().BeFalse(
			"a successful edit on the sibling surface must clear the transposed changed flag too");
		canonicalSurface.RecipeRows[0].IsChanged(RecipeTestDriver.TaskColumn).Should().BeFalse();
	}

	[AvaloniaFact]
	public void ClickAwayClearOnCanonicalSurface_ClearsChangedFlag_OnTransposedColumn()
	{
		var canonicalSurface = _fixture.CreateCanonicalSurface();
		canonicalSurface.Initialize();

		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		var transposedCell = GetCell(0, RecipeTestDriver.TaskColumn);
		transposedCell.IsChanged.Should().BeTrue("the action change marks seeded cells on both surfaces");

		canonicalSurface.ClearChangedByClickAway(
			canonicalSurface.RecipeRows[0], RecipeTestDriver.TaskColumn);

		transposedCell.IsChanged.Should().BeFalse(
			"a click-away acknowledgement on the canonical surface must clear the transposed sibling too");
		canonicalSurface.RecipeRows[0].IsChanged(RecipeTestDriver.TaskColumn).Should().BeFalse();
	}

	[AvaloniaFact]
	public void ClickAwayClear_WithRowNotInSurface_DoesNotClearAnySurface()
	{
		var canonicalSurface = _fixture.CreateCanonicalSurface();
		canonicalSurface.Initialize();

		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);
		var detachedRow = _surface.StepColumns[0].Row;
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);
		_surface.StepColumns[0].Row.Should().NotBeSameAs(detachedRow);

		_surface.ClearChangedByClickAway(detachedRow, RecipeTestDriver.TaskColumn);

		GetCell(0, RecipeTestDriver.TaskColumn).IsChanged.Should().BeTrue(
			"a row that left the projection must not clear the step now occupying its index");
		canonicalSurface.RecipeRows[0].IsChanged(RecipeTestDriver.TaskColumn).Should().BeTrue();
	}

	[AvaloniaFact]
	public void OnMutation_InRangeSignals_DoNotLogStaleWarnings()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveSteps(new[] { 0, 2 });
		_fixture.Coordinator.RemoveStep(0);

		_logger.Entries.Should().NotContain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("Stale"));
	}

	private ParameterCellViewModel GetCell(int columnIndex, string parameterKey)
	{
		return _surface.StepColumns[columnIndex].Cells
			.Single(cell => cell.Descriptor.ParameterKey == parameterKey);
	}
}
