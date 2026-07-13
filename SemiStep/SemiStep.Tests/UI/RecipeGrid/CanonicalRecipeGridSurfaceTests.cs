using System.Collections.Immutable;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class CanonicalRecipeGridSurfaceTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private readonly RecordingLogger<CanonicalRecipeGridSurface> _logger = new();
	private CanonicalRecipeGridSurface _surface = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_surface = _fixture.CreateCanonicalSurface(_logger);
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

		_surface.RecipeRows.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void AppendStep_AddsOneRow()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void AppendStep_RowHasCorrectActionId()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.RecipeRows[0].ActionId.Should().Be(RecipeTestDriver.WaitActionId);
	}

	[AvaloniaFact]
	public void AppendStep_RowStepNumberIsOne_ForFirstRow()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.RecipeRows[0].StepNumber.Should().Be(1);
	}

	[AvaloniaFact]
	public void InsertStep_InsertsRowAtCorrectIndex()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.InsertStep(1, RecipeTestDriver.ForLoopActionId);

		_surface.RecipeRows[1].ActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[AvaloniaFact]
	public void InsertStep_RenumbersSubsequentRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.ForLoopActionId);

		_surface.RecipeRows[1].StepNumber.Should().Be(2);
		_surface.RecipeRows[2].StepNumber.Should().Be(3);
	}

	[AvaloniaFact]
	public void RemoveStep_ReducesRowCount()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveStep(0);

		_surface.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void RemoveStep_RenumbersRemainingRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveStep(0);

		_surface.RecipeRows[0].StepNumber.Should().Be(1);
		_surface.RecipeRows[1].StepNumber.Should().Be(2);
	}

	[AvaloniaFact]
	public void RemoveSteps_RemovesMultipleRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveSteps(new[] { 0, 2 });

		_surface.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void RemoveSteps_RenumbersRemainingRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveSteps(new[] { 0, 1 });

		_surface.RecipeRows[0].StepNumber.Should().Be(1);
	}

	[AvaloniaFact]
	public void ChangeStepAction_RebuildsRow_WithNewActionId()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		_surface.RecipeRows[0].ActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[AvaloniaFact]
	public void NewRecipe_ClearsAllRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.NewRecipe();

		_surface.RecipeRows.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void FullRebuild_RowCountMatchesRecipeStepCount()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.Undo();
		_fixture.Coordinator.Undo();

		_surface.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void SelectedStepIndex_DerivedFromSelectedStepIndices_FirstElement()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.UpdateSelection(new[] { 1 });

		_surface.SelectedStepIndex.Should().Be(1);
	}

	[AvaloniaFact]
	public void SelectedStepIndex_NegativeOne_WhenNoSelection()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.UpdateSelection(Array.Empty<int>());

		_surface.SelectedStepIndex.Should().Be(-1);
	}

	[AvaloniaFact]
	public void RequestSelection_EmitsOnSelectionRequests()
	{
		_fixture.Coordinator.NewRecipe();
		int? captured = -100;
		using var subscription = _surface.SelectionRequests.Subscribe(index => captured = index);

		_surface.RequestSelection(5);

		captured.Should().Be(5);
	}

	[AvaloniaFact]
	public void RequestSelection_WithNull_EmitsNullOnSelectionRequests()
	{
		_fixture.Coordinator.NewRecipe();
		int? captured = -100;
		using var subscription = _surface.SelectionRequests.Subscribe(index => captured = index);

		_surface.RequestSelection(null);

		captured.Should().BeNull();
	}

	[AvaloniaFact]
	public void CanDeleteStep_False_Initially()
	{
		_fixture.Coordinator.NewRecipe();

		bool? latest = null;
		using var subscription = _surface.CanDeleteStep.Subscribe(value => latest = value);

		latest.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CanDeleteStep_True_WhenRowSelected()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		bool? latest = null;
		using var subscription = _surface.CanDeleteStep.Subscribe(value => latest = value);

		_surface.UpdateSelection(new[] { 0 });

		latest.Should().BeTrue();
	}

	[AvaloniaFact]
	public void CollectSelectedSteps_ReturnsStepsInIndexOrder()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.UpdateSelection(new[] { 2, 0 });

		var steps = _surface.CollectSelectedSteps();

		steps.Should().HaveCount(2);
		var recipe = _fixture.Coordinator.CurrentRecipe;
		steps[0].Should().Be(recipe.Steps[0]);
		steps[1].Should().Be(recipe.Steps[2]);
	}

	[AvaloniaFact]
	public void PropertyUpdated_UpdatesRowInPlace_WithoutChangingCount()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "15");

		_surface.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void StepStartTimes_RefreshedAfterMutation()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.RecipeRows[0].StepStartTime.Should().NotBeNull();
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
	public void OnMutation_StepAppended_OutOfRange_LogsWarningAndDoesNotThrow()
	{
		_fixture.Coordinator.NewRecipe();

		var act = () => _surface.OnMutation(new MutationSignal.StepAppended(50));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepAppended"));
	}

	[AvaloniaFact]
	public void OnMutation_StepsInserted_OutOfRange_LogsWarningAndDoesNotThrow()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(new MutationSignal.StepsInserted(10, 3));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepsInserted"));
	}

	[AvaloniaFact]
	public void OnMutation_StepActionChanged_OutOfRange_LogsWarningAndDoesNotThrow()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(new MutationSignal.StepActionChanged(42));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepActionChanged"));
	}

	[AvaloniaFact]
	public void OnMutation_StepAppended_InRange_DoesNotLogWarning()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_logger.Entries.Should().NotContain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("Stale"));
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
	public void OnMutation_StepRemoved_NegativeIndex_LogsWarningAndDoesNotThrow()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(new MutationSignal.StepRemoved(-1));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepRemoved"));
	}

	[AvaloniaFact]
	public void OnMutation_StepsRemoved_AnyIndexOutOfRange_LogsWarningAndDoesNotThrow()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var act = () => _surface.OnMutation(
			new MutationSignal.StepsRemoved(ImmutableArray.Create(0, 99)));

		act.Should().NotThrow();
		_logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("StepsRemoved"));
	}

	[AvaloniaFact]
	public void OnMutation_StepRemoved_InRange_DoesNotLogWarning()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveStep(0);

		_logger.Entries.Should().NotContain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("Stale"));
	}

	[AvaloniaFact]
	public void OnMutation_StepsRemoved_InRange_DoesNotLogWarning()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveSteps(new[] { 0, 2 });

		_logger.Entries.Should().NotContain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("Stale"));
	}

	[AvaloniaFact]
	public void ChangeStepAction_MarksChangedColumns_ToNewStepPropertyKeys()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		var expected = _fixture.Coordinator.CurrentRecipe.Steps[0].Properties.Keys
			.Select(id => id.Value)
			.ToList();
		_surface.RecipeRows[0].ChangedColumns.Should().BeEquivalentTo(expected);
	}

	[AvaloniaFact]
	public void AppendStep_DoesNotMarkChangedColumns()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.RecipeRows[0].ChangedColumns.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void PropertyEdit_ClearsChangedColumn_AfterSuccessfulUpdate()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var row = _surface.RecipeRows[0];
		row.MarkChanged(new[] { RecipeTestDriver.StepDurationColumn });

		row.SetPropertyValue(RecipeTestDriver.StepDurationColumn, "15");

		row.ChangedColumns.Should().NotContain(RecipeTestDriver.StepDurationColumn);
	}

	[AvaloniaFact]
	public void PropertyEdit_RejectedValue_PreservesChangedColumn()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var row = _surface.RecipeRows[0];
		row.MarkChanged(new[] { RecipeTestDriver.StepDurationColumn });

		// "999999" exceeds the duration property maximum, so UpdateStepProperty returns IsFailed and
		// OnCellValueChanged takes the early-return before ClearChanged: a rejected edit keeps orange.
		row.SetPropertyValue(RecipeTestDriver.StepDurationColumn, "999999");

		row.ChangedColumns.Should().Contain(RecipeTestDriver.StepDurationColumn);
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
			_surface.RecipeRows[i].IsPastStep.Should().BeTrue($"step {i} should be past");
			_surface.RecipeRows[i].IsCurrentStep.Should().BeFalse();
		}

		_surface.RecipeRows[3].IsCurrentStep.Should().BeTrue();
		_surface.RecipeRows[3].IsPastStep.Should().BeFalse();

		for (var i = 4; i <= 7; i++)
		{
			_surface.RecipeRows[i].IsPastStep.Should().BeFalse($"step {i} stale past flag must be cleared");
			_surface.RecipeRows[i].IsCurrentStep.Should().BeFalse();
		}
	}
}
