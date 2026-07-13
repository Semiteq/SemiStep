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
		FindComboBox(stepListBox, 0, ActionColumnKey).Focus();

		PressKey(PhysicalKey.ArrowRight);

		_surface.SelectedStepIndex.Should().Be(1);
		((ListBoxItem)stepListBox.ContainerFromIndex(1)!).IsSelected.Should().BeTrue();
		FocusedElement().Should().BeSameAs(FindComboBox(stepListBox, 1, ActionColumnKey));
	}

	[AvaloniaFact]
	public void Left_SelectsPreviousColumn_AndFocusesSameParameterRow()
	{
		var stepListBox = ShowView();
		FindComboBox(stepListBox, 1, ActionColumnKey).Focus();

		PressKey(PhysicalKey.ArrowLeft);

		_surface.SelectedStepIndex.Should().Be(0);
		FocusedElement().Should().BeSameAs(FindComboBox(stepListBox, 0, ActionColumnKey));
	}

	[AvaloniaFact]
	public void Left_AtFirstColumn_IsConsumedWithoutCyclingComboValue()
	{
		var stepListBox = ShowView();
		var actionCombo = FindComboBox(stepListBox, 0, ActionColumnKey);
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
		FindComboBox(stepListBox, 0, RecipeTestDriver.TargetColumn).Focus();

		PressKey(PhysicalKey.ArrowRight);

		_surface.SelectedStepIndex.Should().Be(1);
		FocusedElement().Should().BeSameAs(
			stepListBox.ContainerFromIndex(1),
			"the fallback must focus the ListBoxItem so native list navigation stays available");
	}

	[AvaloniaFact]
	public void Down_FocusesNextParameterCell_InSameColumn()
	{
		var stepListBox = ShowView();
		FindComboBox(stepListBox, 0, ActionColumnKey).Focus();

		PressKey(PhysicalKey.ArrowDown);

		FocusedElement().Should().BeSameAs(FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn));
		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(
			RecipeTestDriver.WaitActionId, "Down must navigate, not cycle the combo value");
	}

	[AvaloniaFact]
	public void Down_SkipsNonFocusableCell()
	{
		var stepListBox = ShowView();
		FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Focus();

		PressKey(PhysicalKey.ArrowDown);

		FindTextBox(stepListBox, 0, RecipeTestDriver.TaskColumn).IsEnabled
			.Should().BeFalse("the task cell is inapplicable for a wait step, so Down must skip it");
		FocusedElement().Should().BeSameAs(FindTextBox(stepListBox, 0, RecipeTestDriver.CommentColumn));
	}

	[AvaloniaFact]
	public void Up_FocusesPreviousParameterCell_InSameColumn()
	{
		var stepListBox = ShowView();
		FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Focus();

		PressKey(PhysicalKey.ArrowUp);

		FocusedElement().Should().BeSameAs(FindComboBox(stepListBox, 0, ActionColumnKey));
	}

	[AvaloniaFact]
	public void LeftRight_InsideFocusedTextBox_StayWithCaret()
	{
		var stepListBox = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Focus();

		PressKey(PhysicalKey.ArrowRight);
		PressKey(PhysicalKey.ArrowLeft);

		FocusedElement().Should().BeSameAs(editor);
		_surface.SelectedStepIndex.Should().Be(-1, "caret movement must not change column selection");
	}

	[AvaloniaFact]
	public void Down_FromTextBox_CommitsPendingEditByDefocusing()
	{
		var stepListBox = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Focus();
		editor.Text = "45";

		PressKey(PhysicalKey.ArrowDown);

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(45f);
		FocusedElement().Should().BeSameAs(FindTextBox(stepListBox, 0, RecipeTestDriver.CommentColumn));
	}

	[AvaloniaFact]
	public void ArrowKeys_InsideOpenComboBoxDropdown_AreLeftAlone()
	{
		var stepListBox = ShowView();
		var actionCombo = FindComboBox(stepListBox, 0, ActionColumnKey);
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

	private static ComboBox FindComboBox(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<ComboBox>()
			.Single(comboBox => comboBox.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
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

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();

		return stepListBox!;
	}
}
