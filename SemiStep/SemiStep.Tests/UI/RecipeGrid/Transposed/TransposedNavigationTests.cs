using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedNavigationTests : IAsyncLifetime
{
	private const int SeededStepCount = 3;
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
	public void Right_SelectsNextColumn_AndFocusesSameParameterRow()
	{
		var stepListBox = ShowView();
		FindComboPresenter(stepListBox, 0, ActionColumnKey).Focus();

		PressKey(PhysicalKey.ArrowRight);

		_surface.SelectedStepIndex.Should().Be(1);
		((ListBoxItem)stepListBox.ContainerFromIndex(1)!).IsSelected.Should().BeTrue();
		FocusedElement().Should().BeSameAs(FindComboPresenter(stepListBox, 1, ActionColumnKey));
	}

	[AvaloniaFact]
	public void Left_SelectsPreviousColumn_AndFocusesSameParameterRow()
	{
		var stepListBox = ShowView();
		FindComboPresenter(stepListBox, 1, ActionColumnKey).Focus();

		PressKey(PhysicalKey.ArrowLeft);

		_surface.SelectedStepIndex.Should().Be(0);
		FocusedElement().Should().BeSameAs(FindComboPresenter(stepListBox, 0, ActionColumnKey));
	}

	[AvaloniaFact]
	public void Left_AtFirstColumn_IsConsumedWithoutCyclingComboValue()
	{
		var stepListBox = ShowView();
		var actionCombo = FindComboPresenter(stepListBox, 0, ActionColumnKey);
		actionCombo.Focus();

		PressKey(PhysicalKey.ArrowLeft);

		FocusedElement().Should().BeSameAs(actionCombo);
		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(RecipeTestDriver.WaitActionId);
	}

	[AvaloniaFact]
	public void Right_WhenNeighborCellNotFocusable_FocusesColumnContainer()
	{
		// Step 0 gets a group-bound action (target combo applicable and focusable); step 1
		// stays a wait step (target inapplicable, so its combo is not focusable). Combo cells
		// are used because Left/Right inside a TextBox stay with the caret by design.
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.WithGroupActionId);
		var stepListBox = ShowView();
		FindComboPresenter(stepListBox, 0, RecipeTestDriver.TargetColumn).Focus();

		PressKey(PhysicalKey.ArrowRight);

		_surface.SelectedStepIndex.Should().Be(1);
		FocusedElement().Should().BeSameAs(
			stepListBox.ContainerFromIndex(1),
			"the fallback must focus the ListBoxItem so native list navigation stays available");
	}

	[AvaloniaFact]
	public void Down_FromColumnContainer_EntersFirstFocusableCell()
	{
		// Reproduce the container fallback: Right from step 0's target combo lands on step 1's
		// container because the neighbour row's combo is not focusable there.
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.WithGroupActionId);
		var stepListBox = ShowView();
		FindComboPresenter(stepListBox, 0, RecipeTestDriver.TargetColumn).Focus();
		PressKey(PhysicalKey.ArrowRight);
		FocusedElement().Should().BeSameAs(stepListBox.ContainerFromIndex(1));

		PressKey(PhysicalKey.ArrowDown);

		FocusedElement().Should().BeSameAs(
			FindComboPresenter(stepListBox, 1, ActionColumnKey),
			"Down from a focused column container must enter the column's first focusable cell");
	}

	[AvaloniaFact]
	public void Right_FromColumnContainer_MovesToNextColumn()
	{
		var stepListBox = ShowView();
		((ListBoxItem)stepListBox.ContainerFromIndex(0)!).Focus();

		PressKey(PhysicalKey.ArrowRight);

		_surface.SelectedStepIndex.Should().Be(1);
		FocusedElement().Should().BeSameAs(FindComboPresenter(stepListBox, 1, ActionColumnKey));
	}

	[AvaloniaFact]
	public void Down_FocusesNextParameterCell_InSameColumn()
	{
		var stepListBox = ShowView();
		FindComboPresenter(stepListBox, 0, ActionColumnKey).Focus();

		PressKey(PhysicalKey.ArrowDown);

		FocusedElement().Should().BeSameAs(FindTextPresenter(stepListBox, 0, RecipeTestDriver.StepDurationColumn));
		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(
			RecipeTestDriver.WaitActionId, "Down must navigate, not cycle the combo value");
	}

	[AvaloniaFact]
	public void Down_SkipsNonFocusableCell()
	{
		var stepListBox = ShowView();
		FindTextPresenter(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Focus();

		PressKey(PhysicalKey.ArrowDown);

		FindTextPresenter(stepListBox, 0, RecipeTestDriver.TaskColumn).IsEnabled
			.Should().BeFalse("the task cell is inapplicable for a wait step, so Down must skip it");
		FocusedElement().Should().BeSameAs(FindTextPresenter(stepListBox, 0, RecipeTestDriver.CommentColumn));
	}

	[AvaloniaFact]
	public void Up_FocusesPreviousParameterCell_InSameColumn()
	{
		var stepListBox = ShowView();
		FindTextPresenter(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Focus();

		PressKey(PhysicalKey.ArrowUp);

		FocusedElement().Should().BeSameAs(FindComboPresenter(stepListBox, 0, ActionColumnKey));
	}

	[AvaloniaFact]
	public void LeftRight_InsideFocusedTextBox_StayWithCaret()
	{
		var stepListBox = ShowView();
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		PressKey(PhysicalKey.ArrowRight);
		PressKey(PhysicalKey.ArrowLeft);

		FocusedElement().Should().BeSameAs(editor);
		_surface.SelectedStepIndex.Should().Be(-1, "caret movement must not change column selection");
	}

	[AvaloniaFact]
	public void Down_FromTextBox_CommitsPendingEditByDefocusing()
	{
		var stepListBox = ShowView();
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Text = "45";

		PressKey(PhysicalKey.ArrowDown);

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(45f);
		FocusedElement().Should().BeSameAs(FindTextPresenter(stepListBox, 0, RecipeTestDriver.CommentColumn));
	}

	[AvaloniaFact]
	public void ArrowKeys_InsideOpenComboBoxDropdown_AreLeftAlone()
	{
		var stepListBox = ShowView();
		var actionCombo = EnterComboEdit(stepListBox, 0, ActionColumnKey);
		actionCombo.IsDropDownOpen = false;
		Dispatcher.UIThread.RunJobs();

		actionCombo.Focus();
		actionCombo.IsDropDownOpen = true;
		Dispatcher.UIThread.RunJobs();

		PressKey(PhysicalKey.ArrowDown);

		var focused = FocusedElement() as Control;
		focused!.FindLogicalAncestorOfType<ComboBox>(includeSelf: true)
			.Should().BeSameAs(actionCombo, "dropdown navigation must stay inside the combo");
	}

	private void PressKey(PhysicalKey key)
	{
		_window!.KeyPressQwerty(key, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();
	}

	private object? FocusedElement()
	{
		return _window!.FocusManager!.GetFocusedElement();
	}

	private static TextBox FindTextBox(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<TextBox>()
			.Single(textBox => textBox.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
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

	// Enters edit on a lazy property-text cell through the real focus + F2 gesture, returning the editor.
	private TextBox EnterTextEdit(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		FindTextPresenter(stepListBox, columnIndex, parameterKey).Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		return FindTextBox(stepListBox, columnIndex, parameterKey);
	}

	private static ComboBox FindComboBox(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<ComboBox>()
			.Single(comboBox => comboBox.DataContext is ParameterCellViewModel cell
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

	// Enters edit on a lazy combo cell through the real focus + F2 gesture (which opens the dropdown),
	// returning the now-built ComboBox.
	private ComboBox EnterComboEdit(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		FindComboPresenter(stepListBox, columnIndex, parameterKey).Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		return FindComboBox(stepListBox, columnIndex, parameterKey);
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

		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		// Exercise the recycle-in-place panel (the production template swap lands in Task 5).
		stepListBox!.UseTransposedColumnsPanel();

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}
}
