using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

// Pins the OnSelectionChanged contract after the O(S*N) IndexOf scan was replaced with
// StepListBox.Selection.SelectedIndexes: the surface's SelectedStepIndices must equal the
// selection model's SelectedIndexes (ascending, index-aligned with StepColumns) after any
// event that fires SelectionChanged.
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedSelectionIndexTests : IAsyncLifetime
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
	public void MultiSelect_NonContiguousColumnsAddedOutOfOrder_PropagatesAscendingIndices()
	{
		var stepListBox = ShowView();

		Select(stepListBox, 4);
		Select(stepListBox, 0);
		Select(stepListBox, 2);

		_surface.SelectedStepIndices.Should().Equal(0, 2, 4);
		_surface.SelectedStepIndices.Should().Equal(stepListBox.Selection.SelectedIndexes);
	}

	[AvaloniaFact]
	public void Deselect_OneColumnFromMultiSelection_PrunesIndices()
	{
		var stepListBox = ShowView();

		Select(stepListBox, 0);
		Select(stepListBox, 2);
		Select(stepListBox, 4);

		// Removing a selected item raises SelectionChanged with RemovedItems.
		Deselect(stepListBox, 2);

		_surface.SelectedStepIndices.Should().Equal(0, 4);
		_surface.SelectedStepIndices.Should().Equal(stepListBox.Selection.SelectedIndexes);
	}

	[AvaloniaFact]
	public void SelectAll_ThenDeselectOne_UpdatesIndices()
	{
		var stepListBox = ShowView();

		stepListBox.SelectAll();
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(0, 1, 2, 3, 4, 5);

		Deselect(stepListBox, 3);

		_surface.SelectedStepIndices.Should().Equal(0, 1, 2, 4, 5);
		_surface.SelectedStepIndices.Should().Equal(stepListBox.Selection.SelectedIndexes);
	}

	[AvaloniaFact]
	public void RemoveSelectedStep_MutationThatFiresSelectionChanged_KeepsContractWithSelectionModel()
	{
		var stepListBox = ShowView();

		Select(stepListBox, 0);
		Select(stepListBox, 2);
		Select(stepListBox, 4);

		// Removing a selected step drops the item from StepColumns; the selection model removes it
		// and raises SelectionChanged, so the surface re-reads Selection.SelectedIndexes.
		_fixture.Coordinator.RemoveStep(2);
		Dispatcher.UIThread.RunJobs();

		// Concrete pin: dropping selected step 2 out of {0, 2, 4} leaves 0 in place and shifts 4 down to 3.
		// A shift/reconcile bug that corrupted both views identically would slip past a bare view==view check.
		_surface.SelectedStepIndices.Should().Equal(0, 3);
		_surface.SelectedStepIndices.Should().Equal(stepListBox.Selection.SelectedIndexes);
	}

	[AvaloniaFact]
	public void OnSelectionChanged_MaterializesIndices_SnapshotDoesNotTrackLaterSelectionChanges()
	{
		var stepListBox = ShowView();

		Select(stepListBox, 0);
		Select(stepListBox, 2);

		// The surface stores what OnSelectionChanged handed it. Selection.SelectedIndexes is a LIVE view
		// over the model's ranges; if the handler aliased it instead of calling ToList(), this snapshot
		// would mutate under us when the selection changes again.
		var snapshot = _surface.SelectedStepIndices;

		Select(stepListBox, 4);

		snapshot.Should().Equal(new[] { 0, 2 }, "the captured index list must be a frozen materialization, not a live view");
	}

	[AvaloniaFact]
	public void InsertStepBeforeSelection_DoesNotRaiseSelectionChanged_LeavesSurfaceIndicesUntouched()
	{
		var stepListBox = ShowView();

		Select(stepListBox, 3);
		Select(stepListBox, 4);
		Select(stepListBox, 5);

		var beforeInsert = _surface.SelectedStepIndices.ToList();

		// An index-shifting insert raises IndexesChanged, not SelectionChanged, so OnSelectionChanged
		// never runs and the surface indices stay as they were. The stale-after-insert gap is a
		// recorded follow-up; this test only pins that the insert does not route through OnSelectionChanged.
		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(beforeInsert);
	}

	private static void Select(ListBox stepListBox, int index)
	{
		stepListBox.SelectedItems!.Add(((TransposedRecipeGridSurface)stepListBox.DataContext!).StepColumns[index]);
		Dispatcher.UIThread.RunJobs();
	}

	private static void Deselect(ListBox stepListBox, int index)
	{
		stepListBox.SelectedItems!.Remove(((TransposedRecipeGridSurface)stepListBox.DataContext!).StepColumns[index]);
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

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox!.DataContext = _surface;

		return stepListBox;
	}
}
