using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

// Reproduces the stale-index bug: when a step is inserted or removed at/before a selected step,
// the cached SelectedStepIndices must shift so it keeps pointing at the same logical steps.
// These are red tests: they assert the correct post-fix values and therefore fail against the
// current unfixed surface, which does not shift the cache on an insert that raises no
// SelectionChanged.
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedSelectionShiftTests : IAsyncLifetime
{
	private const int SeededStepCount = 6;

	private readonly UIFixture _fixture = new();
	private TransposedRecipeGridSurface _surface = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);

		_surface = _fixture.CreateTransposedSurface();
		_surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void InsertStepBeforeSelection_ShiftsCachedIndexUp()
	{
		var stepListBox = ShowView();

		Select(stepListBox, 2);

		// Inserting at index 0 pushes the selected step from index 2 to index 3.
		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(3);
	}

	[AvaloniaFact]
	public void RemoveStepBeforeSelection_ShiftsSurvivorDown()
	{
		_surface.UpdateSelection(new[] { 3 });

		// Removing step 1 (before the selection) shifts the surviving step 3 down to 2.
		_fixture.Coordinator.RemoveStep(1);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(2);
	}

	[AvaloniaFact]
	public void InsertStepAfterSelection_LeavesCacheUnchanged()
	{
		_surface.UpdateSelection(new[] { 1 });

		_fixture.Coordinator.InsertStep(4, RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(1);
	}

	[AvaloniaFact]
	public void RemoveStepAfterSelection_LeavesCacheUnchanged()
	{
		_surface.UpdateSelection(new[] { 1 });

		_fixture.Coordinator.RemoveStep(4);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(1);
	}

	[AvaloniaFact]
	public void InsertBeforeContiguousRange_ShiftsBlockUp()
	{
		_surface.UpdateSelection(new[] { 2, 3, 4 });

		// Inserting one step before the whole block moves {2, 3, 4} up to {3, 4, 5}.
		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(3, 4, 5);
	}

	[AvaloniaFact]
	public void Undo_RecipeReplaced_ClearsCache()
	{
		// An append leaves undo history without disturbing the selection at index 2.
		_fixture.Coordinator.InsertStep(SeededStepCount, RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();

		_surface.UpdateSelection(new[] { 2 });

		// RecipeReplaced from a full rebuild invalidates every index, so the cache clears.
		_fixture.Coordinator.Undo();
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void ChangeStepAction_OnSelectedStep_LeavesCacheUnchanged()
	{
		_surface.UpdateSelection(new[] { 2 });

		// StepActionChanged replaces the row at a stable index; the shift must not touch the cache.
		// The live re-selection is governed by OnActionChanged -> RequestSelection, not the shift.
		_fixture.Coordinator.ChangeStepAction(2, RecipeTestDriver.PauseActionId);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(2);
	}

	[AvaloniaFact]
	public void InsertBeforeThenRemoveSelection_DeletesOriginallySelectedStep()
	{
		// Give the selected step a unique content signature so identity is provable by value.
		_fixture.Coordinator.UpdateStepProperty(2, RecipeTestDriver.StepDurationColumn, "777");
		Dispatcher.UIThread.RunJobs();
		var originallySelectedStep = _fixture.Coordinator.CurrentRecipe.Steps[2];

		_surface.UpdateSelection(new[] { 2 });

		// Inserting before the selection shifts the cache from [2] to [3].
		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();
		_surface.SelectedStepIndices.Should().Equal(3);

		// Deleting via the shifted cache must remove the originally selected step, not a bystander.
		_fixture.Coordinator.RemoveSteps(_surface.SelectedStepIndices);
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.CurrentRecipe.Steps.Should().NotContain(originallySelectedStep);
	}

	[AvaloniaFact]
	public void RemoveSelectedStep_LiveControl_CacheAgreesWithSelectionModel()
	{
		var stepListBox = ShowView();

		Select(stepListBox, 0);
		Select(stepListBox, 2);
		Select(stepListBox, 4);

		// The remove drops a selected step, so the ListBox pushes SelectionChanged mid-mutation.
		// The surface shift and the control's pushed indices must agree.
		_fixture.Coordinator.RemoveStep(2);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(0, 3);
		_surface.SelectedStepIndices.Should().Equal(stepListBox.Selection.SelectedIndexes);
	}

	private static void Select(ListBox stepListBox, int index)
	{
		stepListBox.SelectedItems!.Add(((TransposedRecipeGridSurface)stepListBox.DataContext!).StepColumns[index]);
		Dispatcher.UIThread.RunJobs();
	}

	private ListBox ShowView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = 1200,
			Height = 800,
			Content = view,
		};

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox!.DataContext = _surface;
		stepListBox.UseTransposedColumnsPanel();

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}
}
