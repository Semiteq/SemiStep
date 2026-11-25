using System.Windows.Input;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "ChangeHighlight")]
[Trait("Category", "Integration")]
public sealed class ChangeMarkUndoTests : IAsyncLifetime
{
	private const string TaskDefaultText = "1";
	private const string OtherTaskValue = "5";

	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private ActiveRecipeGridSurface _grid = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
		_grid = (ActiveRecipeGridSurface)_viewModel.RecipeGrid;
		_fixture.SeedRecipe(3);
		_grid.Initialize();
	}

	public ValueTask DisposeAsync()
	{
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	private ICommand ClearCommand => _viewModel.RecipeCommands.ClearChangeMarksCommand;

	private Recipe CurrentRecipe => _fixture.Coordinator.CurrentRecipe;

	[AvaloniaFact]
	public void UndoOfAnUnrelatedEdit_KeepsTheActionChangeMarks()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		EditCell(1, RecipeTestDriver.CommentColumn, "abc");

		_fixture.Coordinator.Undo();

		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);
	}

	[AvaloniaFact]
	public void UndoAndRedoOfAnActionChange_ShowEachRowWithTheMarksItHad()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		ChangeAction(0, RecipeTestDriver.WithGroupActionId);
		var marksAfterSecondChange = MarksOf(0);
		marksAfterSecondChange.Should().NotBeEmpty("precondition: the second action change marks cells");

		_fixture.Coordinator.Undo();

		CurrentRecipe.Steps[0].ActionKey.Should().Be(RecipeTestDriver.ForLoopActionId);
		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);

		_fixture.Coordinator.Redo();

		CurrentRecipe.Steps[0].ActionKey.Should().Be(RecipeTestDriver.WithGroupActionId);
		ShouldHaveMarks(0, [.. marksAfterSecondChange]);
	}

	[AvaloniaFact]
	public void UndoOfAnEditToAMarkedCell_MarksTheCellAgain()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		EditCell(0, RecipeTestDriver.TaskColumn, OtherTaskValue);
		ShouldHaveMarks(0);

		_fixture.Coordinator.Undo();

		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);
	}

	[AvaloniaFact]
	public void UndoOfARowDelete_BringsTheRowBackMarked()
	{
		ChangeAction(1, RecipeTestDriver.ForLoopActionId);

		_fixture.Coordinator.RemoveStep(1);
		_fixture.Coordinator.Undo();

		ShouldHaveMarks(1, RecipeTestDriver.TaskColumn);
	}

	[AvaloniaFact]
	public void PasteUndoRedo_LeavesPastedRowsUnmarked_AndKeepsTheOtherMarks()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		var pastedStep = CurrentRecipe.Steps[1];

		_fixture.Coordinator.InsertSteps(1, [pastedStep]);

		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);
		ShouldHaveMarks(1);

		_fixture.Coordinator.Undo();

		CurrentRecipe.StepCount.Should().Be(3);
		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);

		_fixture.Coordinator.Redo();

		CurrentRecipe.StepCount.Should().Be(4);
		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);
		ShouldHaveMarks(1);
	}

	[AvaloniaFact]
	public async Task CsvLoad_StartsWithoutMarks()
	{
		var path = Path.Combine(Path.GetTempPath(), $"SemiStep.ChangeMarkUndo.{Guid.NewGuid():N}.csv");
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		ChangeAction(1, RecipeTestDriver.EndForLoopActionId);
		for (var stepIndex = 0; stepIndex < CurrentRecipe.StepCount; stepIndex++)
		{
			// CSV drops an empty comment, so only a recipe without one loads back equal.
			EditCell(stepIndex, RecipeTestDriver.CommentColumn, "abc");
		}

		var markedRecipe = CurrentRecipe;
		MarksOf(0).Should().NotBeEmpty("precondition: the saved state has marks");
		(await _fixture.Coordinator.SaveRecipeAsync(path)).IsSuccess.Should().BeTrue();

		try
		{
			(await _fixture.Coordinator.LoadRecipeAsync(path)).IsSuccess.Should().BeTrue();

			CurrentRecipe.Should().Be(markedRecipe, "precondition: the load gives a recipe equal to the marked one");
			CurrentRecipe.Should().NotBeSameAs(markedRecipe);
			ShouldShowNoMarks();
		}
		finally
		{
			File.Delete(path);
		}
	}

	[AvaloniaFact]
	public async Task PlcLoad_StartsWithoutMarks()
	{
		_fixture.S7Service.RecipeToReturn = new Recipe([.. CurrentRecipe.Steps]);
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);

		(await _fixture.Coordinator.LoadRecipeFromPlcAsync()).IsSuccess.Should().BeTrue();

		ShouldShowNoMarks();
	}

	[AvaloniaFact]
	public void NewRecipe_StartsWithoutMarks()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);

		_fixture.Coordinator.NewRecipe();

		ShouldShowNoMarks();
	}

	[AvaloniaFact]
	public async Task ConflictResolvedWithThePlcRecipe_StartsWithoutMarks()
	{
		var waitStep = CurrentRecipe.Steps[0];
		var plcRecipe = new Recipe([waitStep, waitStep, waitStep, waitStep]);
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		_viewModel.ResolveConflictInteraction.RegisterHandler(context => context.SetOutput(false));
		_fixture.S7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 4);
		_fixture.S7Service.RecipeToReturn = plcRecipe;
		(await _fixture.Coordinator.EnableSync()).IsSuccess.Should().BeTrue();

		_fixture.S7Service.RaiseStateChanged(PlcConnectionState.Connected);
		await TestHelpers.WaitUntilAsync(
			() => ReferenceEquals(CurrentRecipe, plcRecipe),
			cancellationToken: TestContext.Current.CancellationToken);

		ShouldShowNoMarks();
	}

	[AvaloniaFact]
	public async Task ReconnectApply_StartsWithoutMarks()
	{
		var plcRecipe = new Recipe([.. CurrentRecipe.Steps]);
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		_fixture.Coordinator.RemoveSteps([0, 1, 2]);
		_fixture.S7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 3);
		_fixture.S7Service.RecipeToReturn = plcRecipe;
		(await _fixture.Coordinator.EnableSync()).IsSuccess.Should().BeTrue();

		_fixture.S7Service.RaiseStateChanged(PlcConnectionState.Connected);
		await TestHelpers.WaitUntilAsync(
			() => ReferenceEquals(CurrentRecipe, plcRecipe),
			cancellationToken: TestContext.Current.CancellationToken);

		ShouldShowNoMarks();
	}

	[AvaloniaFact]
	public void ClearAllThenUndo_AddsNoUndoStep_AndShowsTheEarlierStateWithItsMarks()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		ChangeAction(1, RecipeTestDriver.ForLoopActionId);
		var canUndoBefore = _fixture.Coordinator.CanUndo;
		var dirtyBefore = _fixture.Coordinator.IsDirty;

		ClearCommand.Execute(null);

		_fixture.Coordinator.CanUndo.Should().Be(canUndoBefore);
		_fixture.Coordinator.IsDirty.Should().Be(dirtyBefore);

		_fixture.Coordinator.Undo();

		CurrentRecipe.Steps[1].ActionKey.Should().Be(RecipeTestDriver.WaitActionId);
		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);
		ShouldHaveMarks(1);
	}

	[AvaloniaFact]
	public void ClearAllEditUndo_ShowsTheClearedStateUnmarked()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		ClearCommand.Execute(null);
		EditCell(1, RecipeTestDriver.CommentColumn, "abc");

		_fixture.Coordinator.Undo();

		ShouldShowNoMarks();
	}

	[AvaloniaFact]
	public void EqualRecipesInHistory_KeepSeparateMarks()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		var afterActionChange = CurrentRecipe;
		CurrentRecipe.Steps[0].Properties[new PropertyId(RecipeTestDriver.TaskColumn)].AsFloat()
			.Should().Be(1f, "precondition: the action change seeds the default");

		EditCell(0, RecipeTestDriver.TaskColumn, OtherTaskValue);
		EditCell(0, RecipeTestDriver.TaskColumn, TaskDefaultText);

		CurrentRecipe.Should().Be(afterActionChange, "precondition: the edit back gives an equal recipe");
		CurrentRecipe.Should().NotBeSameAs(afterActionChange);

		_fixture.Coordinator.Undo();
		_fixture.Coordinator.Undo();

		CurrentRecipe.Should().BeSameAs(afterActionChange, "undo restores the instance that history stored");
		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);

		_fixture.Coordinator.Redo();
		_fixture.Coordinator.Redo();

		CurrentRecipe.Should().Be(afterActionChange).And.NotBeSameAs(afterActionChange);
		ShouldShowNoMarks();
	}

	[AvaloniaFact]
	public void RestoredStateClearedThenLeft_ComesBackUnmarked()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		EditCell(1, RecipeTestDriver.CommentColumn, "abc");
		_fixture.Coordinator.Undo();
		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);

		ClearCommand.Execute(null);
		_fixture.Coordinator.Redo();
		_fixture.Coordinator.Undo();

		ShouldShowNoMarks();
	}

	[AvaloniaFact]
	public void ClearCommand_IsEnabledAfterUndoOrRedo_ExactlyWhenMarksAreRestored()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		EditCell(1, RecipeTestDriver.CommentColumn, "abc");
		ClearCommand.Execute(null);

		_fixture.Coordinator.Undo();
		ClearCommand.CanExecute(null).Should().BeTrue("the state after the action change has marks");

		_fixture.Coordinator.Undo();
		ClearCommand.CanExecute(null).Should().BeFalse("the seeded state has no marks");

		_fixture.Coordinator.Redo();
		ClearCommand.CanExecute(null).Should().BeTrue("the state after the action change has marks");

		_fixture.Coordinator.Redo();
		ClearCommand.CanExecute(null).Should().BeFalse("the edited state was left cleared");
	}

	[AvaloniaFact]
	public void ClickAwayClearThenUndo_BringsTheActionChangeMarksBack()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		EditCell(1, RecipeTestDriver.CommentColumn, "abc");
		var row = _grid.CanonicalSurface.RecipeRows[0];
		foreach (var key in row.ChangedColumns.ToList())
		{
			_grid.CanonicalSurface.ClearChangedByClickAway(row, key);
		}

		ShouldShowNoMarks();

		_fixture.Coordinator.Undo();

		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);

		_fixture.Coordinator.Redo();

		ShouldShowNoMarks();
	}

	[AvaloniaFact]
	public void RunStartAndEndThenUndo_BringsTheActionChangeMarksBack()
	{
		ChangeAction(0, RecipeTestDriver.ForLoopActionId);
		EditCell(1, RecipeTestDriver.CommentColumn, "abc");
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();
		_fixture.SetRecipeActive(false);
		Dispatcher.UIThread.RunJobs();

		ShouldShowNoMarks();

		_fixture.Coordinator.Undo().IsSuccess.Should().BeTrue();

		ShouldHaveMarks(0, RecipeTestDriver.TaskColumn);

		_fixture.Coordinator.Redo().IsSuccess.Should().BeTrue();

		ShouldShowNoMarks();
	}

	private void ChangeAction(int stepIndex, int actionId)
	{
		_fixture.Coordinator.ChangeStepAction(stepIndex, actionId).IsSuccess.Should().BeTrue();
	}

	private void EditCell(int stepIndex, string columnKey, string value)
	{
		var before = CurrentRecipe;

		_grid.CanonicalSurface.RecipeRows[stepIndex].SetPropertyValue(columnKey, value);

		CurrentRecipe.Should().NotBeSameAs(before, "precondition: the edit is accepted");
	}

	private HashSet<string> MarksOf(int stepIndex)
	{
		return [.. _grid.CanonicalSurface.RecipeRows[stepIndex].ChangedColumns];
	}

	private void ShouldHaveMarks(int stepIndex, params string[] keys)
	{
		_grid.CanonicalSurface.RecipeRows[stepIndex].ChangedColumns.Should().BeEquivalentTo(keys, "canonical row");
		_grid.TransposedSurface.StepColumns[stepIndex].Row.ChangedColumns
			.Should().BeEquivalentTo(keys, "transposed row");
	}

	private void ShouldShowNoMarks()
	{
		_grid.CanonicalSurface.RecipeRows.Should().OnlyContain(row => row.ChangedColumns.Count == 0);
		_grid.TransposedSurface.StepColumns.Should().OnlyContain(column => column.Row.ChangedColumns.Count == 0);
		ClearCommand.CanExecute(null).Should().BeFalse();
	}
}
