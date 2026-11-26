using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

using Path = Avalonia.Controls.Shapes.Path;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedComboEditingTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;
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
	[Trait("Area", "ComboOpen")]
	public void ComboCell_RendersDisplayText_NoLiveComboBox_ByDefault()
	{
		var stepListBox = ShowView();
		var waitDisplayText = ActionDisplayText(RecipeTestDriver.WaitActionId);

		ComboDisplayText(stepListBox, 0, ActionColumnKey).Should().Be(
			waitDisplayText, "the display shows the selected action's text without a live ComboBox");
		ContainerComboBoxCount(stepListBox, 0).Should().Be(
			0, "no ComboBox is instantiated until edit entry");
	}

	[AvaloniaFact]
	[Trait("Area", "ComboOpen")]
	public void FirstPressOnUnselectedComboColumn_OpensListBeforeRelease_SelectsColumn_ShowsSelection()
	{
		var (view, stepListBox) = ShowInteractiveView();
		var point = CellPoint(stepListBox, 0, ActionColumnKey);

		_window!.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();
		view.IsEditing.Should().BeTrue("the first plain press on a combo cell enters combo edit");
		FindComboBox(stepListBox, 0, ActionColumnKey).IsDropDownOpen.Should().BeTrue("the press alone opens the list");

		_window!.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		var comboBox = FindComboBox(stepListBox, 0, ActionColumnKey);
		comboBox.IsDropDownOpen.Should().BeTrue("the column selection and container focus keep the list open");
		_surface.SelectedStepIndices.Should().Equal([0], "the opening press selects the column");
		((ComboBoxItemViewModel)comboBox.SelectedItem!).Id.Should().Be(
			RecipeTestDriver.WaitActionId, "the built combo shows the cell's current selection");
	}

	[AvaloniaFact]
	[Trait("Area", "ComboOpen")]
	public void PressOpen_DownThenEnter_PicksNextItem_KeepsColumnSelected()
	{
		var stepListBox = ShowInteractiveView().StepListBox;
		ClickCell(stepListBox, 1, ActionColumnKey);
		var comboBox = FindComboBox(stepListBox, 1, ActionColumnKey);
		var nextActionId = ((ComboBoxItemViewModel)comboBox.Items[comboBox.SelectedIndex + 1]!).Id;

		_window!.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
		_window!.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.CurrentRecipe.Steps[1].ActionKey.Should().Be(nextActionId, "the list keys pick the next item");
		_surface.SelectedStepIndices.Should().Equal([1], "the list keys do not move the column selection");
	}

	[AvaloniaFact]
	[Trait("Area", "ComboOpen")]
	public void PressOpen_Escape_ClosesList_ReturnsFocusToColumnContainer()
	{
		var (view, stepListBox) = ShowInteractiveView();
		ClickCell(stepListBox, 1, ActionColumnKey);
		var actionId = _fixture.Coordinator.CurrentRecipe.Steps[1].ActionKey;

		_window!.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeFalse("Esc closes the list, so Delete and Ctrl+C act on the step again");
		_window!.FocusManager!.GetFocusedElement().Should().BeSameAs(
			stepListBox.ContainerFromIndex(1), "focus returns to the column container");
		_fixture.Coordinator.CurrentRecipe.Steps[1].ActionKey.Should().Be(actionId);
	}

	[AvaloniaFact]
	[Trait("Area", "ComboOpen")]
	public void PressOpen_PressOnListItem_PicksItAndClosesList()
	{
		var stepListBox = ShowInteractiveView().StepListBox;
		ClickCell(stepListBox, 1, ActionColumnKey);
		var comboBox = FindComboBox(stepListBox, 1, ActionColumnKey);
		var targetIndex = comboBox.SelectedIndex + 1;
		var targetActionId = ((ComboBoxItemViewModel)comboBox.Items[targetIndex]!).Id;

		ComboBoxTestHelper.ClickListItem(comboBox, targetIndex);

		_fixture.Coordinator.CurrentRecipe.Steps[1].ActionKey.Should().Be(targetActionId, "a press on an item picks it");
		comboBox.IsDropDownOpen.Should().BeFalse("picking an item closes the list");
	}

	[AvaloniaFact]
	[Trait("Area", "ComboOpen")]
	public void DoublePressOnComboCell_OpensThenClosesList()
	{
		var stepListBox = ShowInteractiveView().StepListBox;

		ClickCell(stepListBox, 1, ActionColumnKey);
		var comboBox = FindComboBox(stepListBox, 1, ActionColumnKey);
		comboBox.IsDropDownOpen.Should().BeTrue("precondition: the first press opens the list");
		ClickCell(stepListBox, 1, ActionColumnKey);

		comboBox.IsDropDownOpen.Should().BeFalse("a second press on the open combo closes it and does not reopen it");
	}

	[AvaloniaFact]
	[Trait("Area", "ComboOpen")]
	public void PlainPressInMultiSelectedColumn_CollapsesSelection_OpensList()
	{
		var (view, stepListBox) = ShowInteractiveView();
		ClickCell(stepListBox, 1, ActionColumnKey, RawInputModifiers.Control);
		ClickCell(stepListBox, 2, ActionColumnKey, RawInputModifiers.Control);
		view.IsEditing.Should().BeFalse("precondition: a Ctrl press only changes the selection");
		_surface.SelectedStepIndices.Should().Equal([1, 2]);

		ClickCell(stepListBox, 2, ActionColumnKey);

		_surface.SelectedStepIndices.Should().Equal([2], "a plain press collapses the selection to its column");
		FindComboBox(stepListBox, 2, ActionColumnKey).IsDropDownOpen.Should().BeTrue("the same press opens the list");
	}

	[AvaloniaFact]
	public void F2OnFocusedComboDisplay_EntersEdit_BuildsComboBox()
	{
		var stepListBox = ShowView();
		var presenter = FindComboPresenter(stepListBox, 0, ActionColumnKey);
		presenter.Focus();

		ContainerComboBoxCount(stepListBox, 0).Should().Be(0, "no editor exists until edit entry");

		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		presenter.IsEditing.Should().BeTrue("F2 on the focused combo display opens the editor");
		FindComboBox(stepListBox, 0, ActionColumnKey).Should().NotBeNull();
	}

	[AvaloniaTheory]
	[Trait("Area", "CellKeyboard")]
	[InlineData(PhysicalKey.F4, RawInputModifiers.None)]
	[InlineData(PhysicalKey.ArrowDown, RawInputModifiers.Alt)]
	public void OpenKeyOnFocusedComboDisplay_OpensList(PhysicalKey key, RawInputModifiers modifiers)
	{
		var stepListBox = ShowInteractiveView().StepListBox;
		var presenter = FindComboPresenter(stepListBox, 0, ActionColumnKey);
		presenter.Focus();
		Dispatcher.UIThread.RunJobs();

		_window!.KeyPressQwerty(key, modifiers);
		Dispatcher.UIThread.RunJobs();

		presenter.IsEditing.Should().BeTrue($"{modifiers} {key} opens the focused combo cell");
		FindComboBox(stepListBox, 0, ActionColumnKey).IsDropDownOpen.Should().BeTrue();
	}

	[AvaloniaFact]
	[Trait("Area", "CellKeyboard")]
	public void PrintableKeyOnFocusedComboDisplay_DoesNothing()
	{
		var stepListBox = ShowInteractiveView().StepListBox;
		var presenter = FindComboPresenter(stepListBox, 0, ActionColumnKey);
		presenter.Focus();
		var actionBefore = _fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey;

		_window!.TypeKey(PhysicalKey.F, "f");
		Dispatcher.UIThread.RunJobs();

		presenter.IsEditing.Should().BeFalse("combo cells ignore typing");
		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(actionBefore);
	}

	// Building the ComboBox on lazy open fires an initial SelectionChanged for the already-selected item;
	// the same-value write no-ops at the model, so it must not mark the recipe dirty or the cell changed.
	[AvaloniaFact]
	public void LazyComboOpen_InitialSelection_ProducesNoRecipeEditOrDirtyMarking()
	{
		var stepListBox = ShowView();
		var row = _surface.StepColumns[0].Row;
		row.IsChanged(ActionColumnKey).Should().BeFalse("precondition: the seeded action cell is not changed");
		var dirtyBefore = _fixture.Coordinator.IsDirty;
		var actionKeyBefore = _fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey;

		EnterComboEdit(stepListBox, 0, ActionColumnKey);

		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(
			actionKeyBefore, "lazily opening the combo must not change the step's action");
		_fixture.Coordinator.IsDirty.Should().Be(
			dirtyBefore, "the initial no-op selection must not mark the recipe dirty");
		row.IsChanged(ActionColumnKey).Should().BeFalse("lazy open must not mark the action cell changed");
		row.ChangedColumns.Should().BeEmpty("lazy open must not mark any cell changed");
	}

	// Counts actual model writes over the whole lazy-open-then-pick flow: the lazy build fires an initial
	// SelectionChanged for the already-selected item (must produce zero mutations) and the user's pick fires
	// another (must produce exactly one), so a double-apply or a spurious extra writeback would be caught.
	[AvaloniaFact]
	public void ComboSelectionChange_WritesBackExactlyOnce()
	{
		var stepListBox = ShowView();
		var mutations = new List<MutationSignal>();
		void OnMutated(MutationSignal signal)
		{
			mutations.Add(signal);
		}

		_fixture.Coordinator.Mutated += OnMutated;
		try
		{
			var comboBox = EnterComboEdit(stepListBox, 0, ActionColumnKey);
			mutations.Should().BeEmpty("the lazy build's initial same-value selection must not write anything");

			var forLoopItem = _fixture.ComboBoxItemsProvider.ActionItems
				.Single(item => item.Id == RecipeTestDriver.ForLoopActionId);

			comboBox.SelectedItem = forLoopItem;
			Dispatcher.UIThread.RunJobs();

			mutations.Should().ContainSingle("the user's pick writes back exactly once")
				.Which.Should().BeOfType<MutationSignal.StepActionChanged>();
		}
		finally
		{
			_fixture.Coordinator.Mutated -= OnMutated;
		}

		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(
			RecipeTestDriver.ForLoopActionId, "changing the selection writes the new action back");
		_surface.StepColumns[0].Row.ChangedColumns.Should().NotBeEmpty(
			"the action change marks the rebuilt column's seeded cells changed");
	}

	// An inapplicable combo (the target selector on a wait step) is non-focusable and blocks edit entry:
	// F2 must not build an editor.
	[AvaloniaFact]
	public void InapplicableCombo_DoesNotEnterEdit()
	{
		var stepListBox = ShowView();
		var presenter = FindComboPresenter(stepListBox, 0, RecipeTestDriver.TargetColumn);
		presenter.IsEnabled.Should().BeFalse("the target combo is inapplicable for a wait step");

		presenter.Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		FindComboPresenter(stepListBox, 0, RecipeTestDriver.TargetColumn).IsEditing.Should().BeFalse(
			"an inapplicable combo must not enter edit");
		ContainerComboBoxCount(stepListBox, 0).Should().Be(0, "no editor is built for a blocked combo");
	}

	// A read-only surface (PLC sync) disables the combo display and blocks edit entry.
	[AvaloniaFact]
	[Trait("Area", "ComboOpen")]
	public void SurfaceReadOnly_Combo_DoesNotEnterEdit()
	{
		var stepListBox = ShowView();
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		var presenter = FindComboPresenter(stepListBox, 0, ActionColumnKey);
		presenter.IsEnabled.Should().BeFalse("a read-only surface disables the combo");

		presenter.Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		FindComboPresenter(stepListBox, 0, ActionColumnKey).IsEditing.Should().BeFalse(
			"a read-only surface must block combo edit entry");
	}

	[AvaloniaFact]
	[Trait("Area", "ComboOpen")]
	public void SurfaceReadOnly_ComboPresses_OpenNoList_SelectColumn_ListKeysChangeNothing()
	{
		var (view, stepListBox) = ShowInteractiveView();
		var actionIdBefore = _fixture.Coordinator.CurrentRecipe.Steps[1].ActionKey;
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		ClickCell(stepListBox, 1, ActionColumnKey);
		ClickCell(stepListBox, 1, ActionColumnKey);

		view.IsEditing.Should().BeFalse("no press opens a locked combo cell");
		ContainerComboBoxCount(stepListBox, 1).Should().Be(0, "a locked press builds no ComboBox");
		_surface.SelectedStepIndices.Should().Equal([1], "a locked press still selects the column");

		_window!.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
		_window!.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.CurrentRecipe.Steps[1].ActionKey.Should().Be(
			actionIdBefore, "the list keys pick nothing in a locked combo cell");
		ComboDisplayText(stepListBox, 1, ActionColumnKey).Should().Be(ActionDisplayText(actionIdBefore));
	}

	[AvaloniaFact]
	[Trait("Area", "ComboLock")]
	public void LockBetweenEditEntryAndDeferredOpen_OpensNoList_ShowsDisplay()
	{
		var (view, stepListBox) = ShowInteractiveView();
		var presenter = FindComboPresenter(stepListBox, 1, ActionColumnKey);
		presenter.Focus();
		Dispatcher.UIThread.RunJobs();

		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		presenter.IsEditing.Should().BeFalse("the deferred open re-reads the lock and ends the edit");
		view.IsEditing.Should().BeFalse();
		presenter.Editor.Should().BeOfType<ComboBox>("F2 built the editor before the lock")
			.Which.IsDropDownOpen.Should().BeFalse("no list opens once the run has started");
		ContainerComboBoxCount(stepListBox, 1).Should().Be(0, "the display shows again");
	}

	[AvaloniaFact]
	[Trait("Area", "ComboLock")]
	public void Chevron_OnFocusedComboCell_NoneWhileLocked()
	{
		var stepListBox = ShowInteractiveView().StepListBox;
		var presenter = FindComboPresenter(stepListBox, 1, ActionColumnKey);
		presenter.Focus();
		Dispatcher.UIThread.RunJobs();

		VisibleChevronOwners(stepListBox).Should().Equal([presenter], "only the focused combo cell shows a chevron");

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		VisibleChevronOwners(stepListBox).Should().BeEmpty("a locked cell shows no chevron");
	}

	private static LazyComboCellPresenter[] VisibleChevronOwners(ListBox stepListBox)
	{
		return [.. stepListBox.GetVisualDescendants()
			.OfType<Path>()
			.Where(path => path.Classes.Contains(ComboBoxCellFactory.ChevronClass) && path.IsEffectivelyVisible)
			.Select(path => path.FindAncestorOfType<LazyComboCellPresenter>()!)];
	}

	[AvaloniaFact]
	[Trait("Area", "ComboOpen")]
	public void Unlock_AfterLock_OnePressOpensList()
	{
		var (view, stepListBox) = ShowInteractiveView();
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();
		_fixture.SetRecipeActive(false);
		Dispatcher.UIThread.RunJobs();

		ClickCell(stepListBox, 1, ActionColumnKey);

		view.IsEditing.Should().BeTrue("after the run stops one press enters combo edit");
		FindComboBox(stepListBox, 1, ActionColumnKey).IsDropDownOpen.Should().BeTrue(
			"after the run stops one press opens the list again");
	}

	// An external value change (an action swap applied through the coordinator) updates the display text
	// even though no ComboBox is live.
	[AvaloniaFact]
	public void ExternalActionChange_UpdatesComboDisplay()
	{
		var stepListBox = ShowView();

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);
		Dispatcher.UIThread.RunJobs();

		ComboDisplayText(stepListBox, 0, ActionColumnKey).Should().Be(
			ActionDisplayText(RecipeTestDriver.ForLoopActionId),
			"a programmatic action change updates the lazy display without a live ComboBox");
	}

	// A container recycled onto a far column must show that column's own selection on its combo display,
	// never a stale value from the column it was built on.
	[AvaloniaFact]
	public void RecycledComboCell_ShowsRebindTargetSelection_NotStale()
	{
		var lastIndex = SeededStepCount - 1;
		_fixture.Coordinator.ChangeStepAction(lastIndex, RecipeTestDriver.ForLoopActionId)
			.IsSuccess.Should().BeTrue();
		var stepListBox = ShowNarrowView();

		ScrollToHorizontalEnd(stepListBox);

		var container = (ListBoxItem)stepListBox.ContainerFromIndex(lastIndex)!;
		ComboDisplayText(container, ActionColumnKey).Should().Be(
			ActionDisplayText(RecipeTestDriver.ForLoopActionId),
			"the recycled slot rebinds its combo display to the rebind-target column's selection");
	}

	// A combo in active edit (ComboBox built, dropdown open) whose container recycles out on a scroll must
	// drop cleanly: the commit-before-rebind closes the dropdown and releases the ComboBox to the display,
	// leaving no live ComboBox anywhere and no stray writeback / dirtying of the recipe.
	[AvaloniaFact]
	public void ActiveComboEditedColumn_RecycledOut_DropsCleanly_NoLeak_NoWriteback()
	{
		var stepListBox = ShowNarrowView();
		var comboBox = EnterComboEdit(stepListBox, 0, ActionColumnKey);
		comboBox.IsDropDownOpen.Should().BeTrue("F2 entry opens the dropdown");
		var actionKeyBefore = _fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey;
		var dirtyBefore = _fixture.Coordinator.IsDirty;

		ScrollToHorizontalEnd(stepListBox);

		LiveComboBoxCount(stepListBox).Should().Be(
			0, "recycling the edited column out releases the ComboBox back to the display, leaking none");
		comboBox.IsDropDownOpen.Should().BeFalse(
			"the commit-before-rebind closes the dropdown so the pooled ComboBox cannot orphan an open popup");
		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(
			actionKeyBefore, "recycling an edited combo out must not write the action back");
		_fixture.Coordinator.IsDirty.Should().Be(
			dirtyBefore, "recycling an edited combo out must not mark the recipe dirty");
	}

	private static int LiveComboBoxCount(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<ComboBox>().Count();
	}

	private string ActionDisplayText(int actionId)
	{
		return _fixture.ComboBoxItemsProvider.ActionItems
			.Single(item => item.Id == actionId)
			.DisplayText;
	}

	private void ClickCell(
		ListBox stepListBox,
		int columnIndex,
		string parameterKey,
		RawInputModifiers modifiers = RawInputModifiers.None)
	{
		var clickPoint = CellPoint(stepListBox, columnIndex, parameterKey);

		_window!.MouseDown(clickPoint, MouseButton.Left, modifiers);
		_window!.MouseUp(clickPoint, MouseButton.Left, modifiers);
		Dispatcher.UIThread.RunJobs();
	}

	private Point CellPoint(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var border = FindCellBorder(stepListBox, columnIndex, parameterKey);
		var point = border.TranslatePoint(new Point(3, 3), _window!);
		point.Should().NotBeNull();

		return point!.Value;
	}

	private static Border FindCellBorder(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<Border>()
			.Single(border => border.Classes.Contains("transposed-cell")
				&& border.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private static LazyComboCellPresenter FindComboPresenter(
		ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<LazyComboCellPresenter>()
			.Single(presenter => presenter.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private static ComboBox FindComboBox(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<ComboBox>()
			.Single(comboBox => comboBox.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private static int ContainerComboBoxCount(ListBox stepListBox, int columnIndex)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants().OfType<ComboBox>().Count();
	}

	private static string? ComboDisplayText(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		return ComboDisplayText((ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!, parameterKey);
	}

	private static string? ComboDisplayText(ListBoxItem container, string parameterKey)
	{
		var presenter = container.GetVisualDescendants()
			.OfType<LazyComboCellPresenter>()
			.Single(p => p.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);

		return presenter.GetVisualDescendants().OfType<TextBlock>().First().Text;
	}

	private ComboBox EnterComboEdit(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		FindComboPresenter(stepListBox, columnIndex, parameterKey).Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		return FindComboBox(stepListBox, columnIndex, parameterKey);
	}

	private static void ScrollToHorizontalEnd(ListBox stepListBox)
	{
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		scrollViewer.Offset = new Vector(scrollViewer.Extent.Width, 0);
		Dispatcher.UIThread.RunJobs();
	}

	private ListBox ShowView()
	{
		return ShowView(width: 1600, installPalette: false).StepListBox;
	}

	private (TransposedRecipeGridView View, ListBox StepListBox) ShowInteractiveView()
	{
		return ShowView(width: 1600, installPalette: true);
	}

	private ListBox ShowNarrowView()
	{
		return ShowView(width: 560, installPalette: false).StepListBox;
	}

	private (TransposedRecipeGridView View, ListBox StepListBox) ShowView(double width, bool installPalette)
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = width,
			Height = 800,
			Content = view,
		};

		if (installPalette)
		{
			// Production installs the config palette at startup (App.axaml.cs). Without it the cell
			// borders have null backgrounds and are not hit-testable, so pointer tests would miss.
			CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		}

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox.UseTransposedColumnsPanel();

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return (view, stepListBox);
	}
}
