using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
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
/// Gate for the in-place child recycle that <see cref="TransposedStepListBox"/> enables: a container
/// scrolled out and reused for a DIFFERENT column must keep the SAME item <c>ContentPresenter.Child</c>
/// and the SAME <see cref="TransposedColumnCellsHost"/> instance (no subtree rebuild), while its header
/// step-number text now shows the new column's value (no stale content from the skipped base clear).
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedChildRecycleTests : IAsyncLifetime
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
	public void Recycle_ReusesContentPresenterChildAndHost_OntoDifferentColumn()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);

		// Snapshot each realized container's OWN item ContentPresenter child (the presenter presenting the
		// ListBoxItem's content, not an arbitrary descendant) and its cells host, keyed by the column the
		// container currently shows, so a real recycle onto a different column can be proven by reference.
		var initialContainers = panel.Children.ToList();
		initialContainers.Should().NotBeEmpty("the panel must realize a viewport of columns on load");

		var snapshots = initialContainers.ToDictionary(
			container => container,
			container => new ContainerSnapshot(
				container.DataContext,
				ItemContentPresenter(container).Child!,
				CellsHost(container)));

		ScrollToHorizontalEnd(stepListBox);

		// Pick a container that is now VISIBLE and bound to a DIFFERENT column: the bounded child count at
		// the far end forces at least one initial container to be reused for a far column.
		var recycled = initialContainers.FirstOrDefault(container =>
			container.IsVisible
			&& container.DataContext is StepColumnViewModel
			&& !ReferenceEquals(container.DataContext, snapshots[container].BoundColumn));

		recycled.Should().NotBeNull(
			"scrolling to the far end must reuse at least one initial container onto a different column");

		var snapshot = snapshots[recycled!];
		var newColumn = (StepColumnViewModel)recycled!.DataContext!;

		ReferenceEquals(ItemContentPresenter(recycled).Child, snapshot.PresenterChild).Should().BeTrue(
			"the item ContentPresenter child subtree must be recycled in place, not rebuilt, across a recycle");
		ReferenceEquals(CellsHost(recycled), snapshot.Host).Should().BeTrue(
			"the TransposedColumnCellsHost instance must survive the recycle (no subtree teardown)");

		HeaderText(recycled).Text.Should().Be(
			newColumn.Row.StepNumber.ToString(CultureInfo.CurrentCulture),
			"the recycled child must re-point to the new column, showing its step number (no stale content)");
	}

	// A multi-selection scrolled out through idle and back must come back fully: both the selection model
	// and each re-realized container's IsSelected AND :selected pseudo-class (the visual selection cue).
	[AvaloniaFact]
	public void MultiSelection_SurvivesScrollRoundTrip_RestoresModelAndSelectedPseudoClass()
	{
		var (_, stepListBox) = ShowProductionView();

		stepListBox.SelectedItems!.Add(_surface.StepColumns[0]);
		stepListBox.SelectedItems!.Add(_surface.StepColumns[1]);
		Dispatcher.UIThread.RunJobs();

		ScrollToHorizontalEnd(stepListBox);
		ScrollToHorizontalStart(stepListBox);

		stepListBox.Selection.SelectedIndexes.Should().Contain(
			new[] { 0, 1 }, "both columns stay selected in the model across the scroll round-trip");

		var container0 = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		var container1 = (ListBoxItem)stepListBox.ContainerFromIndex(1)!;

		container0.IsSelected.Should().BeTrue("column 0 re-realized from idle carries its selection");
		container1.IsSelected.Should().BeTrue("column 1 re-realized from idle carries its selection");
		container0.Classes.Contains(":selected").Should().BeTrue(
			"the :selected pseudo-class (the visual cue) is restored on the re-realized container 0");
		container1.Classes.Contains(":selected").Should().BeTrue(
			"the :selected pseudo-class is restored on the re-realized container 1");
	}

	// The exact regression the ClearValue(IsSelectedProperty) on unrealize guards: a container reused from a
	// formerly-SELECTED column onto an UNSELECTED one must come back deselected. Without the clear, its stale
	// IsSelected would be written back into the selection model by ContainerForItemPreparedOverride, bleeding
	// selection onto the new column.
	[AvaloniaFact]
	public void ReusedContainer_FromSelectedColumnOntoUnselected_ComesBackDeselected_WithoutBleed()
	{
		var (_, stepListBox) = ShowProductionView();
		var panel = ColumnsPanel(stepListBox);

		stepListBox.SelectedItems!.Add(_surface.StepColumns[0]);
		stepListBox.SelectedItems!.Add(_surface.StepColumns[1]);
		Dispatcher.UIThread.RunJobs();

		var selectedColumns = new HashSet<object?> { _surface.StepColumns[0], _surface.StepColumns[1] };
		var boundBefore = panel.Children.ToDictionary(container => container, container => container.DataContext);

		ScrollToHorizontalEnd(stepListBox);

		// A container that showed a selected column near the start and now shows a different far column: the
		// bounded child count at the far end forces at least one such reuse.
		var reused = boundBefore.Keys.FirstOrDefault(container =>
			selectedColumns.Contains(boundBefore[container])
			&& container.IsVisible
			&& container.DataContext is StepColumnViewModel
			&& !ReferenceEquals(container.DataContext, boundBefore[container]));

		reused.Should().NotBeNull(
			"scrolling to the far end must reuse a formerly-selected container onto a different column");

		var newColumn = (StepColumnViewModel)reused!.DataContext!;
		selectedColumns.Should().NotContain(newColumn, "the reused container now shows a different, unselected column");

		((ListBoxItem)reused).IsSelected.Should().BeFalse(
			"the recycled container must carry no stale selection onto the newly bound column");
		reused.Classes.Contains(":selected").Should().BeFalse(
			"no stale :selected pseudo-class bleeds onto the reused container");

		var newIndex = _surface.StepColumns.IndexOf(newColumn);
		stepListBox.Selection.SelectedIndexes.Should().NotContain(
			newIndex, "the reused column must not be written into the selection model by an IsSelected bleed");
	}

	// The OnItemsReset reorder invariant: with an active multi-selection, a Reset (RecipeReplaced) must not
	// fire a spurious Selection.Deselect on an old-collection index during the mid-reset container clear.
	// Unmapping the containers before ClearItemContainer keeps SelectingItemsControl.ContainerSelectionChanged
	// a no-op (index -1). What survives is exactly ONE clean model event - the collection emptying, which
	// removes the two formerly-selected columns and adds none - and nothing throws. A spurious mid-reset
	// deselect would show up as an extra event (or an out-of-range throw) on top of that single emptying.
	[AvaloniaFact]
	public void OnItemsReset_WithActiveMultiSelection_DoesNotMutateSelectionModel()
	{
		var (_, stepListBox) = ShowProductionView();

		stepListBox.SelectedItems!.Add(_surface.StepColumns[0]);
		stepListBox.SelectedItems!.Add(_surface.StepColumns[1]);
		Dispatcher.UIThread.RunJobs();

		var selectionChangedCount = 0;
		var removedTotal = 0;
		var addedTotal = 0;
		stepListBox.SelectionChanged += (_, e) =>
		{
			selectionChangedCount++;
			removedTotal += e.RemovedItems.Count;
			addedTotal += e.AddedItems.Count;
		};

		Action replaceRecipe = () =>
		{
			_fixture.Coordinator.NewRecipe();
			Dispatcher.UIThread.RunJobs();
		};

		replaceRecipe.Should().NotThrow(
			"the reordered OnItemsReset must not deselect an old-collection index while the map is torn down");
		selectionChangedCount.Should().Be(
			1, "only the collection emptying fires; no spurious mid-reset deselect is added on top of it");
		removedTotal.Should().Be(2, "the single event removes exactly the two formerly-selected columns");
		addedTotal.Should().Be(0, "the teardown adds nothing to the selection");
		stepListBox.Selection.SelectedIndexes.Should().BeEmpty("the selection ends cleanly empty after the reset");
	}

	// Post-fix commit path: the view's OnContainerClearing hook is the PRIMARY edit-commit path on scroll-out.
	// The subtree stays attached at clearing time, so the hook finds a live TransposedColumnCellsPresenter and
	// commits the pending edit through it - not through the Host.OnDetachedFromVisualTree side-channel (which
	// no longer fires on scroll because the host stays attached). This handler runs AFTER the view's own
	// clearing hook (subscribed on Loaded), so it observes the already-committed state while the presenter is
	// still live in the subtree.
	[AvaloniaFact]
	public void OnContainerClearing_FindsLivePresenter_AndCommitsAtClearingTime()
	{
		var (view, stepListBox) = ShowProductionView();

		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		view.IsEditing.Should().BeTrue("the F2 gesture opened the editor");
		stepListBox.SelectedIndex.Should().Be(-1, "focusing a cell must not select the column");

		var clearingFired = false;
		var committedAtClearing = false;
		TransposedColumnCellsPresenter? presenterAtClearing = null;
		stepListBox.ContainerClearing += (_, e) =>
		{
			if (!ReferenceEquals(e.Container, container))
			{
				return;
			}

			clearingFired = true;
			presenterAtClearing = e.Container.GetVisualDescendants()
				.OfType<TransposedColumnCellsPresenter>()
				.FirstOrDefault();
			committedAtClearing = Equals(_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn], 45f);
		};

		editor.Text = "45";
		ScrollToHorizontalEnd(stepListBox);

		clearingFired.Should().BeTrue("the edited column's container is unrealized (ContainerClearing) on scroll-out");
		presenterAtClearing.Should().NotBeNull(
			"the cells presenter subtree is still attached at clearing time - the hook's live primary commit path");
		committedAtClearing.Should().BeTrue(
			"the OnContainerClearing hook committed the pending edit through the live presenter, at clearing time");
		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			45f, "the pending edit is committed as the column recycles out");
		view.IsEditing.Should().BeFalse("the commit ended the edit; no editor is active after the recycle");
	}

	// Post-fix a recycled container is hidden (IsVisible=false) with its subtree - and any open editor - still
	// ALIVE (the open editor is the TabOnceActiveElement, so the container recycles rather than defers). Focus
	// must not stay parked inside that now-invisible subtree; OnContainerClearing relocates it off before the
	// hide, keeping keyboard navigation live on the grid.
	[AvaloniaFact]
	public void FocusInsideRecycledColumn_DoesNotStayParkedInHiddenSubtree()
	{
		var (view, stepListBox) = ShowProductionView();

		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		view.IsEditing.Should().BeTrue("the F2 gesture opened the editor");
		_window!.FocusManager!.GetFocusedElement().Should().BeSameAs(
			editor, "the open editor holds keyboard focus before the scroll-out");

		ScrollToHorizontalEnd(stepListBox);
		// The relocation is posted from the clearing hook (the ListBox forwards focus to a container, and no
		// stable target exists mid-recycle); drive the deferred job before asserting.
		Dispatcher.UIThread.RunJobs();

		editor.IsFocused.Should().BeFalse(
			"the committed editor is torn out of the subtree on recycle and must not keep keyboard focus");
		_window!.FocusManager!.GetFocusedElement().Should().NotBeSameAs(
			editor, "focus must not stay parked on the recycled column's former editor");
		stepListBox.IsKeyboardFocusWithin.Should().BeTrue(
			"focus relocates onto a visible column so it is not stranded on the detached editor and navigation stays live");

		// Keyboard navigation is live afterwards: with focus stranded at null an arrow key no-ops; restored to
		// a visible column, the grid still owns focus after a navigation keystroke.
		_window!.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		stepListBox.IsKeyboardFocusWithin.Should().BeTrue("the grid still owns focus after a navigation keystroke");
	}

	private static ContentPresenter ItemContentPresenter(Control container)
	{
		return container.GetVisualDescendants()
			.OfType<ContentPresenter>()
			.First(presenter => ReferenceEquals(presenter.TemplatedParent, container));
	}

	private static TransposedColumnCellsHost CellsHost(Control container)
	{
		return container.GetVisualDescendants().OfType<TransposedColumnCellsHost>().First();
	}

	private static TextBlock HeaderText(Control container)
	{
		var headerBorder = container.GetVisualDescendants()
			.OfType<Border>()
			.First(border => border.Classes.Contains("transposed-step-header"));

		return headerBorder.GetVisualDescendants().OfType<TextBlock>().First();
	}

	private static TransposedColumnsPanel ColumnsPanel(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<TransposedColumnsPanel>().Single();
	}

	private static void ScrollToHorizontalEnd(ListBox stepListBox)
	{
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		scrollViewer.Offset = new Avalonia.Vector(scrollViewer.Extent.Width, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private static void ScrollToHorizontalStart(ListBox stepListBox)
	{
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		scrollViewer.Offset = new Avalonia.Vector(0, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private static TransposedTextCellPresenter FindTextPresenter(
		ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<TransposedTextCellPresenter>()
			.Single(presenter => presenter.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private static TextBox FindTextBox(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<TextBox>()
			.Single(textBox => textBox.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private TextBox EnterTextEdit(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		FindTextPresenter(stepListBox, columnIndex, parameterKey).Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		return FindTextBox(stepListBox, columnIndex, parameterKey);
	}

	// Mirrors TransposedCommitOnClearingTests: no injector, so the real .axaml wiring (the TransposedStepListBox
	// subclass + TransposedColumnsPanel) and the view's WhenActivated clearing/selection hooks are exercised.
	private (TransposedRecipeGridView View, ListBox StepListBox) ShowProductionView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();

		_window = new Window
		{
			Width = NarrowWindowWidth,
			Height = 800,
			Content = view,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return (view, stepListBox!);
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

	private readonly record struct ContainerSnapshot(
		object? BoundColumn,
		Control PresenterChild,
		TransposedColumnCellsHost Host);
}
