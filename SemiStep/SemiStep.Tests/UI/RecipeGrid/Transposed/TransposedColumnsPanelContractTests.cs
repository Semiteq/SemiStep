using System;
using System.Collections.Generic;
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
/// Risk-gate contract tests for <see cref="TransposedColumnsPanel"/>. They prove the keep-attached
/// recycle holds under Avalonia's headless ListBox: containers are reused (never detached) across
/// scroll, the realized set stays viewport-bound, the generator prepare/clear hooks fire, the
/// selection-anchor container is deferred rather than unrealized while it is the TabOnceActiveElement,
/// and multi-selection round-trips through idle without loss.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedColumnsPanelContractTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;
	private const double NarrowWindowWidth = 560;

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
	public void Scroll_ReusesSameContainers_WithoutDetaching()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);

		var detached = new List<object?>();
		var tracked = new HashSet<Control>();

		void TrackChildren()
		{
			foreach (var child in panel.Children)
			{
				if (tracked.Add(child))
				{
					child.DetachedFromVisualTree += (sender, _) => detached.Add(sender);
				}
			}
		}

		TrackChildren();
		var initialContainers = panel.Children.ToList();
		initialContainers.Should().NotBeEmpty("the panel must realize a viewport of columns on load");

		// Record which data item each initial container is bound to, so we can prove a real recycle:
		// a bounded child count at the far end forces at least one of these containers to be reused for
		// a DIFFERENT column, which a never-virtualizing panel keeping all 40 attached would never do.
		var boundColumnBefore = initialContainers.ToDictionary(container => container, container => container.DataContext);

		ScrollToHorizontalEnd(stepListBox);
		TrackChildren();

		initialContainers.Should().Contain(
			container => !ReferenceEquals(container.DataContext, boundColumnBefore[container]) && container.IsVisible,
			"scrolling to the far end must rebind at least one initial container onto a different column (a real recycle)");

		ScrollToHorizontalStart(stepListBox);
		TrackChildren();

		detached.Should().BeEmpty("keep-attached recycle must never detach a container during scroll");
		foreach (var container in initialContainers)
		{
			panel.Children.Should().Contain(
				container, "a scrolled-out container is hidden and reused, not discarded and rebuilt");
		}
	}

	[AvaloniaFact]
	public void Scroll_KeepsChildrenCountViewportBound()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);

		ScrollToHorizontalEnd(stepListBox);
		ScrollToHorizontalStart(stepListBox);
		ScrollToHorizontalEnd(stepListBox);

		// The peak attached child count is the columns spanning the viewport plus the panel's fixed buffer
		// on each side. Derive the bound from the actual viewport so a partial-recycle leak (idle containers
		// piling up well past the real steady state) is caught, not masked by a loose fraction of the recipe.
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		var viewportColumns = (int)Math.Ceiling(scrollViewer.Viewport.Width / panel.ColumnWidth);
		const int BufferColumnsPerSide = 2;
		const int Slack = 3;
		var maxExpectedChildren = viewportColumns + (2 * BufferColumnsPerSide) + Slack;

		panel.Children.Count.Should().BeLessThanOrEqualTo(
			maxExpectedChildren,
			"peak attached children are the viewport span plus a fixed two-column buffer each side, never a growing idle pile");
	}

	[AvaloniaFact]
	public void RealizeUnrealize_FiresGeneratorHooks_AndResyncsSelectionOnRealize()
	{
		var stepListBox = ShowView();

		var preparedCount = 0;
		var clearingCount = 0;
		stepListBox.ContainerPrepared += (_, _) => preparedCount++;
		stepListBox.ContainerClearing += (_, _) => clearingCount++;

		stepListBox.SelectedItems!.Add(_surface.StepColumns[0]);
		stepListBox.SelectedItems!.Add(_surface.StepColumns[1]);
		Dispatcher.UIThread.RunJobs();

		ScrollToHorizontalEnd(stepListBox);
		ScrollToHorizontalStart(stepListBox);

		preparedCount.Should().BeGreaterThan(0, "realizing recycled containers must fire ContainerPrepared");
		clearingCount.Should().BeGreaterThan(0, "unrealizing containers must fire ContainerClearing");

		((ListBoxItem)stepListBox.ContainerFromIndex(0)!).IsSelected.Should().BeTrue(
			"a selected column re-realized after scrolling out must re-sync IsSelected from the selection model");
		((ListBoxItem)stepListBox.ContainerFromIndex(1)!).IsSelected.Should().BeTrue(
			"the second selected column stays selected on its container across the round-trip");
	}

	// Gate-critical: the selection-anchor container is the TabOnceActiveElement. It must be deferred
	// (kept attached and visible) while scrolled out, so an editor/focus it holds survives, and only
	// unrealized once the anchor moves elsewhere.
	[AvaloniaFact]
	public void AnchorContainer_IsDeferredWhileScrolledOut_AndReleasedWhenAnchorMoves()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);

		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();

		var anchor = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		KeyboardNavigation.GetTabOnceActiveElement(stepListBox).Should().BeSameAs(
			anchor, "selecting a column makes its container the TabOnceActiveElement anchor");

		// Exercise the focus path the deferral protects: focusing then hiding must not throw or detach.
		anchor.Focus();

		var clearedContainers = new List<Control>();
		stepListBox.ContainerClearing += (_, e) => clearedContainers.Add(e.Container);

		ScrollToHorizontalEnd(stepListBox);

		panel.Children.Should().Contain(anchor, "the deferred anchor stays attached offscreen");
		anchor.IsVisible.Should().BeTrue("the deferred anchor stays visible so its editor/focus survives");
		clearedContainers.Should().NotContain(
			anchor, "the anchor must not be unrealized while it is the TabOnceActiveElement");
		stepListBox.ContainerFromIndex(0).Should().BeSameAs(anchor, "the deferred anchor stays resolvable by index");

		var farIndex = SeededStepCount - 1;
		_surface.RequestSelection(farIndex);
		Dispatcher.UIThread.RunJobs();

		clearedContainers.Should().Contain(anchor, "the former anchor is unrealized once the anchor moves away");
		anchor.IsVisible.Should().BeFalse("the released container is hidden into the idle pool");
	}

	// Gate-critical: multi-selection must round-trip through idle. Two columns selected, scrolled out,
	// the model touched, then scrolled back must stay selected in both the model and their containers.
	[AvaloniaFact]
	public void MultiSelection_SurvivesScrollRoundTrip_ThroughIdle()
	{
		var stepListBox = ShowView();

		stepListBox.SelectedItems!.Add(_surface.StepColumns[0]);
		stepListBox.SelectedItems!.Add(_surface.StepColumns[1]);
		Dispatcher.UIThread.RunJobs();

		ScrollToHorizontalEnd(stepListBox);

		// Touch the selection model while columns 0 and 1 are idle/deferred offscreen.
		stepListBox.SelectedItems!.Add(_surface.StepColumns[SeededStepCount - 1]);
		Dispatcher.UIThread.RunJobs();
		stepListBox.SelectedItems!.Remove(_surface.StepColumns[SeededStepCount - 1]);
		Dispatcher.UIThread.RunJobs();

		ScrollToHorizontalStart(stepListBox);

		stepListBox.Selection.SelectedIndexes.Should().Contain(
			new[] { 0, 1 }, "both columns stay selected in the model across the scroll round-trip");
		((ListBoxItem)stepListBox.ContainerFromIndex(0)!).IsSelected.Should().BeTrue(
			"column 0 re-realized from idle must show selected");
		((ListBoxItem)stepListBox.ContainerFromIndex(1)!).IsSelected.Should().BeTrue(
			"column 1 re-realized from idle must show selected");
	}

	private static TransposedColumnsPanel ColumnsPanel(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<TransposedColumnsPanel>().Single();
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
