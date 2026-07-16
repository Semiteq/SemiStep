using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

// Task 4: lazy display/editor swap for ComboBox cells. A combo cell renders a display TextBlock showing
// the selected item's text by default and builds the heavy ComboBox only on edit entry through the shared
// edit coordinator, releasing it back to the display on exit.
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
	public void SecondClickOnSelectedComboColumn_EntersEdit_BuildsComboBox_ShowsSelection()
	{
		var (view, stepListBox) = ShowInteractiveView();

		ClickCell(stepListBox, 0, ActionColumnKey);
		view.IsEditing.Should().BeFalse("a plain first click selects the column without editing");

		ClickCell(stepListBox, 0, ActionColumnKey);

		view.IsEditing.Should().BeTrue("the second click on the selected column enters combo edit");
		var comboBox = FindComboBox(stepListBox, 0, ActionColumnKey);
		((ComboBoxItemViewModel)comboBox.SelectedItem!).Id.Should().Be(
			RecipeTestDriver.WaitActionId, "the built combo shows the cell's current selection");
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

			var forLoopItem = _fixture.RecipeMetadataRegistry.GetActionComboBoxItems()
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
		return _fixture.RecipeMetadataRegistry.GetActionComboBoxItems()
			.Single(item => item.Id == actionId)
			.DisplayText;
	}

	private void ClickCell(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var border = FindCellBorder(stepListBox, columnIndex, parameterKey);
		var clickPoint = border.TranslatePoint(new Point(3, 3), _window!);
		clickPoint.Should().NotBeNull();

		_window!.MouseDown(clickPoint!.Value, MouseButton.Left, RawInputModifiers.None);
		_window!.MouseUp(clickPoint.Value, MouseButton.Left, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();
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

	private static TransposedComboCellPresenter FindComboPresenter(
		ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<TransposedComboCellPresenter>()
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
			.OfType<TransposedComboCellPresenter>()
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
		// Exercise the recycle-in-place panel (the production template swap lands in Task 5).
		stepListBox!.UseTransposedColumnsPanel();

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return (view, stepListBox);
	}
}
