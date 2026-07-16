using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

// Confirms the cell selection background is sourced from TransposedColumnCellsPresenter.IsColumnSelected
// (fed imperatively from the container ListBoxItem) and no longer from a RelativeSource ancestor lookup:
// selecting paints, deselecting reverts, a presenter recycled out of a selected column paints unselected,
// and a scroll-sweep plus a select logs zero Avalonia binding errors (the ancestor-not-found storm is gone).
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedSelectionBindingTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;
	private const string CellClass = "transposed-cell";

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
	public void SelectedColumn_EditableCell_PaintsSelectionBackground()
	{
		var stepListBox = ShowView(1200);
		var index = EditableApplicableIndex();

		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();

		var cells = FindCellBorders(stepListBox, 0);
		cells[index].Background.Should().BeSameAs(Resource(CellPaletteInstaller.SelectionBackgroundBrushKey));
	}

	[AvaloniaFact]
	public void DeselectingColumn_RevertsEditableCellBackground()
	{
		var stepListBox = ShowView(1200);
		var index = EditableApplicableIndex();

		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();

		var cells = FindCellBorders(stepListBox, 0);
		cells[index].Background.Should().BeSameAs(
			Resource(CellPaletteInstaller.SelectionBackgroundBrushKey), "selecting the column paints its cells");

		_surface.RequestSelection(null);
		Dispatcher.UIThread.RunJobs();

		cells[index].Background.Should().BeSameAs(
			Resource(CellPaletteInstaller.GridBackgroundBrushKey),
			"deselecting must revert the background, proving the imperative sync propagates change notifications");
	}

	[AvaloniaFact]
	public void MultipleColumnsSelected_BothPaintSelectionBackground()
	{
		var stepListBox = ShowView(1200);
		var index = EditableApplicableIndex(0, 1);
		var selectionBrush = Resource(CellPaletteInstaller.SelectionBackgroundBrushKey);

		stepListBox.SelectedItems!.Add(_surface.StepColumns[0]);
		stepListBox.SelectedItems!.Add(_surface.StepColumns[1]);
		Dispatcher.UIThread.RunJobs();

		FindCellBorders(stepListBox, 0)[index].Background.Should().BeSameAs(
			selectionBrush, "the first selected column paints its cells");
		FindCellBorders(stepListBox, 1)[index].Background.Should().BeSameAs(
			selectionBrush, "a second column selected simultaneously (SelectionMode=Multiple) paints its cells too");
	}

	[AvaloniaFact]
	public void ContainerRecycledOutOfSelectedColumn_PaintsUnselected()
	{
		var stepListBox = ShowView(560);
		var index = EditableApplicableIndex();
		var selectionBrush = Resource(CellPaletteInstaller.SelectionBackgroundBrushKey);

		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();

		FindCellBorders(stepListBox, 0)[index].Background.Should().BeSameAs(
			selectionBrush, "column 0 is selected before the scroll");

		ScrollToHorizontalEnd(stepListBox);

		// Proves pool recycling actually happened: a non-virtualizing layout would realize all columns and
		// never reuse the selected column's released presenter, making the stale-carry check below vacuous.
		RealizedContainerCount(stepListBox).Should().BeLessThan(
			SeededStepCount / 2, "the far columns must be hosted by recycled containers, not freshly realized ones");

		// The recycled containers now host far, unselected columns; no pooled presenter released from the
		// selected column may carry its selection into the next column it is bound to.
		var unselectedFarColumnsChecked = 0;
		foreach (var container in stepListBox.GetRealizedContainers())
		{
			if (container is ListBoxItem { IsSelected: false } item)
			{
				FindCellBorders(item)[index].Background.Should().NotBeSameAs(
					selectionBrush, "an unselected recycled column must not paint the selection brush");
				unselectedFarColumnsChecked++;
			}
		}

		unselectedFarColumnsChecked.Should().BeGreaterThan(
			0, "the scroll must land on far, unselected columns hosted by reused presenters, else the stale-carry path is untested");

		ScrollToHorizontalStart(stepListBox);

		FindCellBorders(stepListBox, 0)[index].Background.Should().BeSameAs(
			selectionBrush,
			"scrolling the still-selected column back in must re-apply selection to its recycled-in presenter");
	}

	[AvaloniaFact]
	public void ScrollSweepAndSelect_LogsZeroBindingErrors()
	{
		// Install the guard before the first realize so binding errors during initial column realization are also caught.
		using var guard = new BindingErrorGuard();

		var stepListBox = ShowView(560);

		// The ancestor-not-found storm this test guards is triggered BY pool recycle, so the run must
		// actually virtualize; a non-recycling layout would pass the guard vacuously.
		RealizedContainerCount(stepListBox).Should().BeLessThan(
			SeededStepCount / 2, "the 560px window must realize a viewport of columns, not the whole recipe, so pool recycling is exercised");

		ScrollToHorizontalEnd(stepListBox);
		ScrollToHorizontalStart(stepListBox);
		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();
		ScrollToHorizontalEnd(stepListBox);
		ScrollToHorizontalStart(stepListBox);

		RealizedContainerCount(stepListBox).Should().BeLessThan(
			SeededStepCount / 2, "containers must be recycled across the scroll sweep, not accumulated");

		guard.AssertNoBindingErrors();
	}

	// Returns a descriptor index that is editable and applicable in every listed column (defaults to column 0),
	// so its cell paints the plain selection background rather than the inapplicable variant.
	private int EditableApplicableIndex(params int[] columnIndices)
	{
		var columns = columnIndices.Length == 0 ? new[] { 0 } : columnIndices;
		var descriptors = _surface.ParameterDescriptors;
		for (var i = 0; i < descriptors.Count; i++)
		{
			if (descriptors[i].IsReadOnlyParameter)
			{
				continue;
			}

			if (columns.All(column => _surface.StepColumns[column].Row.IsApplicable(descriptors[i].ParameterKey)))
			{
				return i;
			}
		}

		throw new InvalidOperationException("No editable, applicable parameter descriptor found.");
	}

	private static int RealizedContainerCount(ListBox stepListBox)
	{
		return stepListBox.GetRealizedContainers().Count();
	}

	private IBrush Resource(string key)
	{
		_window!.TryFindResource(key, out var value).Should().BeTrue($"resource '{key}' must be installed");
		return value.Should().BeAssignableTo<IBrush>().Subject;
	}

	private static List<Border> FindCellBorders(ListBox stepListBox, int columnIndex)
	{
		return FindCellBorders((ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!);
	}

	private static List<Border> FindCellBorders(ListBoxItem container)
	{
		return container.GetVisualDescendants()
			.OfType<Border>()
			.Where(border => border.Classes.Contains(CellClass))
			.ToList();
	}

	private static void ScrollToHorizontalEnd(ListBox stepListBox)
	{
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		scrollViewer.Offset = new Vector(scrollViewer.Extent.Width, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private static void ScrollToHorizontalStart(ListBox stepListBox)
	{
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		scrollViewer.Offset = new Vector(0, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private ListBox ShowView(double width)
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = width,
			Height = 800,
			Content = view,
		};

		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		ExecutionPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox!.UseTransposedColumnsPanel();

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}
}
