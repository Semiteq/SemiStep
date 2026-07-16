using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

/// <summary>
/// Layout tests for <see cref="TransposedColumnsPanel"/> pinning the exact uniform-width viewport
/// math and the focus-anchor deferral: the realized index range tracks the scroll offset plus a fixed
/// buffer with idle children hidden, the <c>TabOnceActiveElement</c> container stays measured and
/// arranged at its index while scrolled out and is released only when the anchor moves, and the desired
/// extent and per-index arrange positions are exact multiples of the column width.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedColumnsPanelLayoutTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;
	private const double NarrowWindowWidth = 560;

	// Mirrors TransposedColumnsPanel.BufferColumns (private const); the buffer assertions pin this value.
	private const int BufferColumns = 2;

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
	public void RealizedRange_TracksScrollOffsetPlusBuffer_IdleChildrenHidden()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var columnWidth = panel.ColumnWidth;
		var scrollViewer = ScrollViewerOf(stepListBox);

		// Scroll to the middle of a target column so floor(offset / width) is unambiguous under any
		// sub-pixel drift in the effective viewport.
		const int TargetIndex = 10;
		scrollViewer.Offset = new Vector((TargetIndex + 0.5) * columnWidth, 0);
		Dispatcher.UIThread.RunJobs();

		var firstVisible = (int)Math.Floor(scrollViewer.Offset.X / columnWidth);
		firstVisible.Should().Be(TargetIndex, "the half-column offset lands the viewport left edge on the target column");

		var firstRealized = firstVisible - BufferColumns;
		stepListBox.ContainerFromIndex(firstRealized).Should().NotBeNull(
			"the fixed buffer realizes columns ahead of the viewport left edge");
		stepListBox.ContainerFromIndex(firstRealized - 1).Should().BeNull(
			"nothing beyond the buffer is realized on the leading side");
		stepListBox.ContainerFromIndex(0).Should().BeNull(
			"a column scrolled far out of the viewport is not realized");

		var resolved = Enumerable.Range(0, stepListBox.ItemCount)
			.Select(index => stepListBox.ContainerFromIndex(index))
			.Where(container => container is not null)
			.Cast<Control>()
			.ToHashSet();

		foreach (var child in panel.Children)
		{
			if (resolved.Contains(child))
			{
				child.IsVisible.Should().BeTrue("a realized (or deferred) container is visible");
			}
			else
			{
				child.IsVisible.Should().BeFalse("an idle recycled container is hidden and not arranged into view");
			}
		}
	}

	[AvaloniaFact]
	public void AnchorContainer_StaysArrangedWhileDeferred_ReleasedWhenAnchorMoves()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var columnWidth = panel.ColumnWidth;

		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();

		var anchor = (Control)stepListBox.ContainerFromIndex(0)!;
		KeyboardNavigation.GetTabOnceActiveElement(stepListBox).Should().BeSameAs(
			anchor, "selecting column 0 makes its container the TabOnceActiveElement anchor");

		ScrollToHorizontalEnd(stepListBox);

		panel.Children.Should().Contain(anchor, "the deferred anchor stays attached while scrolled out");
		anchor.IsVisible.Should().BeTrue("the deferred anchor stays visible so its focus/editor survives");
		anchor.DesiredSize.Height.Should().BeGreaterThan(0, "the deferred anchor keeps being measured offscreen");
		anchor.Bounds.X.Should().BeApproximately(
			0, 0.5, "the deferred anchor stays arranged at its index position (index 0 -> x = 0)");
		anchor.Bounds.Width.Should().BeApproximately(
			columnWidth, 0.5, "the deferred anchor keeps the uniform column width while laid out offscreen");
		anchor.Bounds.Height.Should().BeGreaterThan(0, "the deferred anchor keeps a real arranged height");

		var farIndex = SeededStepCount - 1;
		_surface.RequestSelection(farIndex);
		Dispatcher.UIThread.RunJobs();

		anchor.IsVisible.Should().BeFalse("the former anchor is unrealized and hidden once the anchor moves away");
		stepListBox.ContainerFromIndex(0).Should().BeNull(
			"the released container drops out of the realized/deferred set into the idle pool");
	}

	[AvaloniaFact]
	public void DesiredWidth_IsCountTimesColumnWidth_ArrangePositionsMatchIndex()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var columnWidth = panel.ColumnWidth;
		var count = stepListBox.ItemCount;

		panel.DesiredSize.Width.Should().BeApproximately(
			count * columnWidth, 0.5, "the exact uniform-width extent is the item count times the column width");
		ScrollViewerOf(stepListBox).Extent.Width.Should().BeApproximately(
			count * columnWidth, 0.5, "the scroll extent follows the panel's exact desired width");

		for (var index = 0; index < count; index++)
		{
			if (stepListBox.ContainerFromIndex(index) is not Control container || !container.IsVisible)
			{
				continue;
			}

			container.Bounds.X.Should().BeApproximately(
				index * columnWidth, 0.5, "each realized column is arranged at index times the column width");
			container.Bounds.Width.Should().BeApproximately(
				columnWidth, 0.5, "each realized column keeps the uniform column width");
		}
	}

	private static TransposedColumnsPanel ColumnsPanel(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<TransposedColumnsPanel>().Single();
	}

	private static ScrollViewer ScrollViewerOf(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
	}

	private static void ScrollToHorizontalEnd(ListBox stepListBox)
	{
		var scrollViewer = ScrollViewerOf(stepListBox);
		scrollViewer.Offset = new Vector(scrollViewer.Extent.Width, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private ListBox ShowView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox!.UseTransposedColumnsPanel();

		_window = new Window
		{
			Width = NarrowWindowWidth,
			Height = 800,
			Content = view,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}
}
