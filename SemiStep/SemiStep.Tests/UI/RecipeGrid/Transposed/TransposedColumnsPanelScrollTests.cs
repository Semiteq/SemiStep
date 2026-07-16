using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

/// <summary>
/// ScrollIntoView and keyboard-navigation tests for <see cref="TransposedColumnsPanel"/>. They pin the
/// eager realization that lands an offscreen column at its exact rect and scrolls it into view: a
/// far-index selection request, the append-then-auto-scroll path, the navigator resolving a neighbour
/// column across the realized-window boundary, and Shift+Right range-extend routing through the panel's
/// <c>GetControl</c> to realize the target past the boundary.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedColumnsPanelScrollTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;
	private const double NarrowWindowWidth = 560;
	private const string ActionColumnKey = "action";

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
	public void FarIndexSelectionRequest_RealizesAndPositionsTargetColumn_InView()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var columnWidth = panel.ColumnWidth;

		var farIndex = SeededStepCount - 1;
		stepListBox.ContainerFromIndex(farIndex).Should().BeNull(
			"the far column starts well outside the realized window");

		_surface.RequestSelection(farIndex);
		Dispatcher.UIThread.RunJobs();

		var container = (Control)stepListBox.ContainerFromIndex(farIndex)!;
		container.Should().NotBeNull("the selection request realizes the far column through ScrollIntoView");
		container.Bounds.X.Should().BeApproximately(
			farIndex * columnWidth, 0.5, "the eagerly realized column sits at its exact index rect");
		container.Bounds.Width.Should().BeApproximately(
			columnWidth, 0.5, "the realized far column keeps the uniform column width");

		var scrollViewer = ScrollViewerOf(stepListBox);
		IsInViewport(container, scrollViewer).Should().BeTrue(
			"the target column is scrolled into the viewport, not just realized offscreen");
	}

	[AvaloniaFact]
	public void AppendStep_AutoScrollToSelected_BringsNewLastColumnIntoView()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var columnWidth = panel.ColumnWidth;
		var scrollViewer = ScrollViewerOf(stepListBox);

		scrollViewer.Offset.X.Should().Be(0, "the view starts scrolled to the first column");

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId).IsSuccess.Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		var newLastIndex = _surface.StepColumns.Count - 1;

		// The add-step flow selects the fresh column; AutoScrollToSelectedItem drives ScrollIntoView.
		_surface.RequestSelection(newLastIndex);
		Dispatcher.UIThread.RunJobs();

		var container = (Control)stepListBox.ContainerFromIndex(newLastIndex)!;
		container.Should().NotBeNull("auto-scroll to the appended column realizes it");
		container.Bounds.X.Should().BeApproximately(
			newLastIndex * columnWidth, 0.5, "the appended column sits at its exact last-index rect");
		scrollViewer.Offset.X.Should().BeGreaterThan(0, "the viewport scrolled toward the appended last column");
		IsInViewport(container, scrollViewer).Should().BeTrue("the appended column is visible after auto-scroll");
	}

	[AvaloniaFact]
	public void NavigatorNeighbourColumn_AcrossRealizationBoundary_ResolvesTargetContainer()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var columnWidth = panel.ColumnWidth;

		FindComboPresenter(stepListBox, 0, ActionColumnKey).Focus();
		Dispatcher.UIThread.RunJobs();

		const int TargetIndex = 20;
		for (var step = 0; step < TargetIndex; step++)
		{
			PressKey(PhysicalKey.ArrowRight, RawInputModifiers.None);
		}

		_surface.SelectedStepIndex.Should().Be(
			TargetIndex, "each Right advanced the neighbour column, walking past the initial realized window");

		var container = (Control)stepListBox.ContainerFromIndex(TargetIndex)!;
		container.Should().NotBeNull("the navigator realized the far target column via ScrollIntoView");
		container.Bounds.X.Should().BeApproximately(
			TargetIndex * columnWidth, 0.5, "the resolved neighbour column sits at its exact rect");
		stepListBox.ContainerFromIndex(0).Should().BeNull(
			"the origin column scrolled out of the realized window during the walk");
		FindComboPresenter(stepListBox, TargetIndex, ActionColumnKey).Should().BeSameAs(
			FocusedElement(), "focus follows the navigator onto the newly realized target column");
	}

	[AvaloniaFact]
	public void ShiftRight_RangeExtend_AcrossRealizationBoundary_ExtendsSelectionViaGetControl()
	{
		var stepListBox = ShowView();

		stepListBox.SelectedIndex = 0;
		((Control)stepListBox.ContainerFromIndex(0)!).Focus();
		Dispatcher.UIThread.RunJobs();

		const int ExtendSteps = 15;
		for (var step = 0; step < ExtendSteps; step++)
		{
			PressKey(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
		}

		var selected = stepListBox.Selection.SelectedIndexes;
		selected.Should().Contain(
			ExtendSteps, "Shift+Right extended the selection past the realized boundary onto the far column");
		selected.Count.Should().Be(
			ExtendSteps + 1, "the whole range from the anchor through the far column is selected");
		stepListBox.ContainerFromIndex(ExtendSteps).Should().NotBeNull(
			"GetControl realized the far target so the ListBox range could extend to it");
	}

	private static bool IsInViewport(Control container, ScrollViewer scrollViewer)
	{
		var viewportLeft = scrollViewer.Offset.X;
		var viewportRight = viewportLeft + scrollViewer.Viewport.Width;

		return container.Bounds.Right > viewportLeft && container.Bounds.X < viewportRight;
	}

	private static TransposedColumnsPanel ColumnsPanel(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<TransposedColumnsPanel>().Single();
	}

	private static ScrollViewer ScrollViewerOf(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
	}

	private static TransposedComboCellPresenter FindComboPresenter(
		ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<TransposedComboCellPresenter>()
			.Single(presenter => presenter.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private void PressKey(PhysicalKey key, RawInputModifiers modifiers)
	{
		_window!.KeyPressQwerty(key, modifiers);
		Dispatcher.UIThread.RunJobs();
	}

	private object? FocusedElement()
	{
		return _window!.FocusManager!.GetFocusedElement();
	}

	private ListBox ShowView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		// Exercise the recycle-in-place panel (the production template swap lands in Task 5).
		stepListBox!.UseTransposedColumnsPanel();

		_window = new Window
		{
			Width = NarrowWindowWidth,
			Height = 800,
			Content = view,
		};

		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}
}
