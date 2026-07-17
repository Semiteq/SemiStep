using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

/// <summary>
/// Items-changed tests for <see cref="TransposedColumnsPanel"/>. They pin the index-shift bookkeeping
/// that keeps realized containers mapped to the right data item across an insert or remove while
/// scrolled into the middle of a large recipe, the extent growth and last-column realization on append,
/// and the Reset teardown that physically detaches every container on a surface swap so the pooled
/// presenters are released.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedColumnsPanelItemsChangedTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;
	private const double NarrowWindowWidth = 560;
	private const int AnchorIndex = 20;

	private readonly UIFixture _fixture = new();
	private TransposedRecipeGridSurface _surface = null!;
	private TransposedRecipeGridView _view = null!;
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
	public void InsertStep_BeforeViewport_ShiftsRealizedContainersToNewIndex_KeepingDataContext()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var columnWidth = panel.ColumnWidth;

		const int TargetIndex = 20;
		ScrollTo(stepListBox, TargetIndex * columnWidth);

		var container = (Control)stepListBox.ContainerFromIndex(TargetIndex)!;
		var boundColumn = container.DataContext;
		boundColumn.Should().BeSameAs(
			_surface.StepColumns[TargetIndex], "the realized container is bound to the column at its index");

		_fixture.Coordinator.InsertStep(5, RecipeTestDriver.WaitActionId).IsSuccess.Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		stepListBox.ContainerFromIndex(TargetIndex + 1).Should().BeSameAs(
			container, "inserting before the viewport shifts the realized container up by one index");
		container.DataContext.Should().BeSameAs(
			boundColumn, "the shifted container keeps its data item — the item moved, the binding did not rebind");
		_surface.StepColumns[TargetIndex + 1].Should().BeSameAs(
			boundColumn, "the same column now lives one index higher after the insert");
	}

	[AvaloniaFact]
	public void RemoveStep_MidScroll_UnrealizesRemovedContainer_AndShiftsSurvivorsDown()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var columnWidth = panel.ColumnWidth;

		const int RemovedIndex = 20;
		ScrollTo(stepListBox, RemovedIndex * columnWidth);

		var removedColumn = _surface.StepColumns[RemovedIndex];
		var survivorColumn = _surface.StepColumns[RemovedIndex + 1];
		stepListBox.ContainerFromIndex(RemovedIndex)!.DataContext.Should().BeSameAs(
			removedColumn, "the column at the removed index is realized before the remove");

		_fixture.Coordinator.RemoveStep(RemovedIndex).IsSuccess.Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns.Should().NotContain(
			removedColumn, "the removed column is gone from the collection");
		_surface.StepColumns[RemovedIndex].Should().BeSameAs(
			survivorColumn, "the survivor after the gap now lives at the removed index");
		((Control)stepListBox.ContainerFromIndex(RemovedIndex)!).DataContext.Should().BeSameAs(
			survivorColumn, "the container now at the removed index binds to the survivor column");

		// The removed item's container must not stay mapped to the gone column: every visible container
		// resolves to a live index and shows exactly that index's column.
		foreach (var child in panel.Children.Where(child => child.IsVisible))
		{
			var index = stepListBox.IndexFromContainer(child);
			index.Should().BeGreaterThanOrEqualTo(0, "a visible container must map to a realized index");
			child.DataContext.Should().BeSameAs(
				_surface.StepColumns[index], "each realized container shows the column at its own index, never a stale one");
		}
	}

	[AvaloniaFact]
	public void AppendStep_GrowsExtent_AndScrollingToEndRealizesNewLastColumn()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var columnWidth = panel.ColumnWidth;

		panel.DesiredSize.Width.Should().BeApproximately(
			SeededStepCount * columnWidth, 0.5, "the initial extent is the seeded count times the column width");

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId).IsSuccess.Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		var newCount = SeededStepCount + 1;
		_surface.StepColumns.Count.Should().Be(newCount);
		panel.DesiredSize.Width.Should().BeApproximately(
			newCount * columnWidth, 0.5, "appending a step grows the desired extent by one column width");

		var newLastIndex = newCount - 1;
		ScrollTo(stepListBox, ScrollViewerOf(stepListBox).Extent.Width);

		var lastContainer = stepListBox.ContainerFromIndex(newLastIndex);
		lastContainer.Should().NotBeNull("scrolling to the max offset realizes the freshly appended last column");
		((Control)lastContainer!).DataContext.Should().BeSameAs(
			_surface.StepColumns[newLastIndex], "the new last column's container binds to the appended step");
	}

	[AvaloniaFact]
	public void RecipeReplaced_TearsDownPanel_DetachingContainers_ThenRebuildsWithoutStaleReuse()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);

		var detached = new List<Control>();
		var containersBefore = panel.Children.ToList();
		containersBefore.Should().NotBeEmpty("the panel realizes a viewport of columns before the swap");
		foreach (var container in containersBefore)
		{
			container.DetachedFromVisualTree += (sender, _) => detached.Add((Control)sender!);
		}

		// A recipe replacement clears the bound collection (a Reset) — the surface-swap teardown trigger.
		// Each detach drives the host's presenter release, so the dying pool reclaims every presenter.
		_fixture.Coordinator.NewRecipe();
		Dispatcher.UIThread.RunJobs();

		detached.Should().Contain(
			containersBefore, "the Reset teardown physically detaches every container so its host releases its presenter");
		panel.Children.Should().BeEmpty("teardown removes every child — realized and idle — from the panel");

		// Rebuild a fresh recipe: the torn-down panel realizes brand-new containers bound to the new
		// columns, never a stale descriptor carried over from the retired recipe.
		const int RebuiltStepCount = 12;
		for (var i = 0; i < RebuiltStepCount; i++)
		{
			_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId).IsSuccess.Should().BeTrue();
		}

		Dispatcher.UIThread.RunJobs();

		var realizedAfter = panel.Children.Where(child => child.IsVisible).ToList();
		realizedAfter.Should().NotBeEmpty("the rebuilt recipe realizes its own viewport of columns");
		foreach (var container in realizedAfter)
		{
			_surface.StepColumns.Should().Contain(
				(StepColumnViewModel)container.DataContext!,
				"every realized container binds to a column from the rebuilt recipe, never a stale one");
		}
	}

	// The deferred selection anchor carries its own index bookkeeping, separate from the realized map.
	// An insert before it must shift its tracked index up so it stays mapped to its (moved) column.
	[AvaloniaFact]
	public void InsertStep_BeforeDeferredAnchor_ShiftsAnchorIndexUp_KeepingItMapped()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var anchor = DeferAnchorAt(stepListBox, panel, AnchorIndex);
		var anchorColumn = anchor.DataContext;

		_fixture.Coordinator.InsertStep(5, RecipeTestDriver.WaitActionId).IsSuccess.Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		const int ShiftedIndex = AnchorIndex + 1;
		stepListBox.ContainerFromIndex(ShiftedIndex).Should().BeSameAs(
			anchor, "inserting before the deferred anchor shifts its tracked index up by one");
		stepListBox.IndexFromContainer(anchor).Should().Be(
			ShiftedIndex, "the deferred anchor reports its new, shifted index");
		anchor.DataContext.Should().BeSameAs(
			anchorColumn, "the shifted anchor keeps its data item — the item moved, the binding did not rebind");
		_surface.StepColumns[ShiftedIndex].Should().BeSameAs(
			anchorColumn, "the anchor's column now lives one index higher after the insert");
		panel.Children.Should().Contain(anchor, "the anchor stays deferred (attached) across the insert");
		anchor.IsVisible.Should().BeTrue("the deferred anchor stays visible so any editor/focus survives");
	}

	// A remove before the deferred anchor must shift its tracked index down so it stays mapped correctly.
	[AvaloniaFact]
	public void RemoveStep_BeforeDeferredAnchor_ShiftsAnchorIndexDown_KeepingItMapped()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var anchor = DeferAnchorAt(stepListBox, panel, AnchorIndex);
		var anchorColumn = anchor.DataContext;

		_fixture.Coordinator.RemoveStep(5).IsSuccess.Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		const int ShiftedIndex = AnchorIndex - 1;
		stepListBox.ContainerFromIndex(ShiftedIndex).Should().BeSameAs(
			anchor, "removing before the deferred anchor shifts its tracked index down by one");
		stepListBox.IndexFromContainer(anchor).Should().Be(
			ShiftedIndex, "the deferred anchor reports its new, shifted index");
		anchor.DataContext.Should().BeSameAs(
			anchorColumn, "the shifted anchor keeps its data item across the remove");
		_surface.StepColumns[ShiftedIndex].Should().BeSameAs(
			anchorColumn, "the anchor's column now lives one index lower after the remove");
		panel.Children.Should().Contain(anchor, "the anchor stays deferred (attached) across the remove");
	}

	// Removing the deferred anchor's own item leaves it mapped to no column, so it must be released:
	// unrealized (ContainerClearing) and recycled to idle, not left dangling as the deferred element.
	[AvaloniaFact]
	public void RemoveStep_OfDeferredAnchorItem_ReleasesTheDeferredAnchor()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);
		var anchor = DeferAnchorAt(stepListBox, panel, AnchorIndex);

		var cleared = new List<Control>();
		stepListBox.ContainerClearing += (_, e) => cleared.Add(e.Container);

		_fixture.Coordinator.RemoveStep(AnchorIndex).IsSuccess.Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		cleared.Should().Contain(
			anchor, "removing the deferred anchor's own item unrealizes it through the clearing hook");
		stepListBox.IndexFromContainer(anchor).Should().Be(
			-1, "the released anchor no longer maps to any index");
		anchor.IsVisible.Should().BeFalse("the released anchor is hidden into the idle pool");
		panel.Children.Should().Contain(anchor, "release recycles the anchor to idle (still attached), it never detaches");
	}

	[AvaloniaFact]
	public void EmptyRecipe_MeasuresZeroExtent_WithNoRealizedContainers_AndNoCrash()
	{
		_fixture.SeedRecipe(0);
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);

		_surface.StepColumns.Should().BeEmpty("the recipe was reset to zero steps");
		panel.DesiredSize.Width.Should().Be(0, "an empty recipe has zero horizontal extent");
		panel.Children.Where(child => child.IsVisible).Should().BeEmpty("no columns means no realized (visible) containers");
		stepListBox.ContainerFromIndex(0).Should().BeNull("there is no column at index 0 to realize");
	}

	[AvaloniaFact]
	public void SingleColumnRecipe_RealizesTheOneColumn_WithExactExtent()
	{
		_fixture.SeedRecipe(1);
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);

		panel.DesiredSize.Width.Should().BeApproximately(
			panel.ColumnWidth, 0.5, "a one-column recipe extends exactly one column width");

		var container = (Control?)stepListBox.ContainerFromIndex(0);
		container.Should().NotBeNull("the sole column realizes");
		container!.DataContext.Should().BeSameAs(_surface.StepColumns[0], "the realized container binds to the only column");
		container.Bounds.Width.Should().BeApproximately(
			panel.ColumnWidth, 0.5, "the single column is arranged at the column width");
		stepListBox.ContainerFromIndex(1).Should().BeNull("there is no second column");
	}

	// Replace is production-reachable: a StepActionChanged rebuilds the column view model at that index
	// (Items[i] = item). The old container must drop (ContainerClearing) and rebind to the fresh column.
	[AvaloniaFact]
	public void ReplaceStep_ViaActionChange_DropsOldContainer_AndRebindsToNewColumn()
	{
		var stepListBox = ShowView();

		const int TargetIndex = 3;
		var oldContainer = (Control)stepListBox.ContainerFromIndex(TargetIndex)!;
		var oldColumn = oldContainer.DataContext;

		var cleared = new List<Control>();
		stepListBox.ContainerClearing += (_, e) => cleared.Add(e.Container);

		_fixture.Coordinator.ChangeStepAction(TargetIndex, RecipeTestDriver.PauseActionId).IsSuccess.Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns[TargetIndex].Should().NotBeSameAs(
			oldColumn, "the action change rebuilds the column view model at that index");
		cleared.Should().Contain(
			oldContainer, "the replaced step's container is unrealized (ContainerClearing) so it rebinds to the new column");
		((Control)stepListBox.ContainerFromIndex(TargetIndex)!).DataContext.Should().BeSameAs(
			_surface.StepColumns[TargetIndex], "the container at the replaced index binds to the fresh column");
	}

	// Move is not emitted by production today (StepColumns only sees Add/Remove/Replace/Reset), but the
	// panel maps it. Moving directly on the bound collection must leave every realized container mapped to
	// the column at its own index — no stale mapping survives the re-key.
	[AvaloniaFact]
	public void MoveStep_WithinRealizedRange_LeavesEveryContainerMappedToItsOwnColumn()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);

		const int FromIndex = 2;
		const int ToIndex = 5;
		var movedColumn = _surface.StepColumns[FromIndex];

		_surface.StepColumns.Move(FromIndex, ToIndex);
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns[ToIndex].Should().BeSameAs(movedColumn, "the moved column now lives at the destination index");
		((Control)stepListBox.ContainerFromIndex(ToIndex)!).DataContext.Should().BeSameAs(
			movedColumn, "the container now at the destination index binds to the moved column");

		foreach (var child in panel.Children.Where(child => child.IsVisible))
		{
			var index = stepListBox.IndexFromContainer(child);
			index.Should().BeGreaterThanOrEqualTo(0, "a visible container must map to a realized index after the move");
			child.DataContext.Should().BeSameAs(
				_surface.StepColumns[index], "each realized container shows the column at its own index, never a stale one");
		}
	}

	private ListBoxItem DeferAnchorAt(ListBox stepListBox, TransposedColumnsPanel panel, int index)
	{
		_surface.RequestSelection(index);
		Dispatcher.UIThread.RunJobs();

		var anchor = (ListBoxItem)stepListBox.ContainerFromIndex(index)!;
		KeyboardNavigation.GetTabOnceActiveElement(stepListBox).Should().BeSameAs(
			anchor, "selecting a column makes its container the TabOnceActiveElement anchor");

		// Scroll back to the start so the selected column leaves the window; the anchor must defer
		// (stay attached and visible), not recycle to idle.
		ScrollTo(stepListBox, 0);

		panel.Children.Should().Contain(anchor, "the selection anchor is deferred (kept attached) while scrolled out");
		anchor.IsVisible.Should().BeTrue("the deferred anchor stays visible so any editor/focus survives");
		stepListBox.ContainerFromIndex(index).Should().BeSameAs(anchor, "the deferred anchor stays resolvable by its index");

		return anchor;
	}

	private static TransposedColumnsPanel ColumnsPanel(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<TransposedColumnsPanel>().Single();
	}

	private static ScrollViewer ScrollViewerOf(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
	}

	private static void ScrollTo(ListBox stepListBox, double horizontalOffset)
	{
		ScrollViewerOf(stepListBox).Offset = new Vector(horizontalOffset, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private ListBox ShowView()
	{
		_view = new TransposedRecipeGridView { DataContext = _surface };
		var stepListBox = _view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox!.UseTransposedColumnsPanel();

		_window = new Window
		{
			Width = NarrowWindowWidth,
			Height = 800,
			Content = _view,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}
}
