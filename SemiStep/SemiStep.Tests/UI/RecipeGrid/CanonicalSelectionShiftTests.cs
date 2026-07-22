using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// Reproduces the stale-index bug on the canonical DataGrid, which fires no SelectionChanged push
// on an index-shifting insert or on a remove that drops selected rows. The cache must still shift
// so it keeps pointing at the same logical steps. These are red tests: they assert the correct
// post-fix values and therefore fail against the current unfixed surface.
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class CanonicalSelectionShiftTests : IAsyncLifetime
{
	private const int SeededStepCount = 6;

	private readonly UIFixture _fixture = new();
	private CanonicalRecipeGridSurface _surface = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);

		_surface = _fixture.CreateCanonicalSurface();
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
		var dataGrid = ShowView();

		dataGrid.SelectedIndex = 2;
		Dispatcher.UIThread.RunJobs();

		// Inserting at index 0 pushes the selected step from index 2 to index 3.
		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(3);
	}

	[AvaloniaFact]
	public void RemoveSelectedStep_FromMultiSelection_ShiftsSurvivorsDownAndDropsRemoved()
	{
		var dataGrid = ShowView();

		dataGrid.SelectedItems.Add(_surface.RecipeRows[0]);
		Dispatcher.UIThread.RunJobs();
		dataGrid.SelectedItems.Add(_surface.RecipeRows[2]);
		Dispatcher.UIThread.RunJobs();
		dataGrid.SelectedItems.Add(_surface.RecipeRows[4]);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(0, 2, 4);

		// Removing step 2 drops it and shifts the surviving step 4 down to 3.
		_fixture.Coordinator.RemoveStep(2);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(0, 3);
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
	public void InsertMultipleStepsBeforeSelection_ShiftsCachedIndexByCount()
	{
		_surface.UpdateSelection(new[] { 4 });

		// Two steps inserted before the selection push the cached index up by the full count, 4 -> 6.
		var stepsToInsert = _fixture.Coordinator.CurrentRecipe.Steps.Take(2).ToList();
		_fixture.Coordinator.InsertSteps(0, stepsToInsert);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(6);
	}

	[AvaloniaFact]
	public void InsertStepAtSelectedIndex_ShiftsCachedIndexUp()
	{
		_surface.UpdateSelection(new[] { 2 });

		// Inserting exactly at the selected index still displaces it: index >= startIndex shifts, 2 -> 3.
		_fixture.Coordinator.InsertStep(2, RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(3);
	}

	[AvaloniaFact]
	public void RemoveSteps_DropsSelectedAndOffsetsSurvivorsPastEachRemovedIndex()
	{
		_surface.UpdateSelection(new[] { 2, 3, 5 });

		// Removing {2, 4}: index 2 is dropped; 3 shifts past one removal to 2; 5 shifts past two removals to 3.
		_fixture.Coordinator.RemoveSteps(new[] { 2, 4 });
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(2, 3);
	}

	[AvaloniaFact]
	public void AppendStep_LeavesSelectionUnchanged()
	{
		_surface.UpdateSelection(new[] { 2 });

		// Appending past the tail adds no index at or before the selection, so the cache is untouched.
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(2);
	}

	private DataGrid ShowView()
	{
		var view = new CanonicalRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = 1200,
			Height = 600,
			Content = view,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		var dataGrid = view.FindControl<DataGrid>("RecipeGrid");
		dataGrid.Should().NotBeNull();

		return dataGrid!;
	}
}
