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

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedEditingTests : IAsyncLifetime
{
	private const int SeededStepCount = 3;

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
	public void TypeAndCommitWithEnter_UpdatesCoordinator()
	{
		var (_, stepListBox) = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		editor.Focus();
		editor.Text = "45";
		_window!.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.CurrentRecipe.Steps[0]
			.Properties.Values.Select(property => property.Value)
			.Should().Contain(45f);
		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(45f);
	}

	[AvaloniaFact]
	public void TimeCell_RendersHms_AndParsesBack()
	{
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "100")
			.IsSuccess.Should().BeTrue();

		var (_, stepListBox) = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		editor.Text.Should().Be("00:01:40");

		editor.Focus();
		editor.Text = "0:2:5";
		_window!.FocusManager!.Focus(null);
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(125f);
		editor.Text.Should().Be("00:02:05");
	}

	[AvaloniaFact]
	public void InvalidTimeInput_StaysUncommitted()
	{
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "100")
			.IsSuccess.Should().BeTrue();

		var (_, stepListBox) = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		editor.Focus();
		editor.Text = "99:99:99";
		_window!.FocusManager!.Focus(null);
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(100f);
		editor.Text.Should().Be(
			"00:01:40", "a rejected edit must snap the editor back to the model's formatted value");
	}

	[AvaloniaFact]
	public void InapplicableCell_EditorIsDisabled()
	{
		var (_, stepListBox) = ShowView();

		FindTextBox(stepListBox, 0, RecipeTestDriver.TaskColumn).IsEnabled.Should().BeFalse();
		FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn).IsEnabled.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ReadOnlyMode_DisablesEditors_AndEditorMustCloseDefocusesActiveOne()
	{
		var (view, stepListBox) = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		editor.Focus();
		view.IsEditing.Should().BeTrue();

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		editor.IsEnabled.Should().BeFalse();
		FindComboBox(stepListBox, 0, "action").IsEnabled.Should().BeFalse();
		view.IsEditing.Should().BeFalse();
		_window!.FocusManager!.GetFocusedElement().Should().NotBeSameAs(editor);

		_fixture.SetRecipeActive(false);
		Dispatcher.UIThread.RunJobs();

		editor.IsEnabled.Should().BeTrue();
	}

	[AvaloniaFact]
	public void IsEditing_TracksEditorFocus()
	{
		var (view, stepListBox) = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		view.IsEditing.Should().BeFalse();

		editor.Focus();
		view.IsEditing.Should().BeTrue();

		_window!.FocusManager!.Focus(null);
		view.IsEditing.Should().BeFalse();
	}

	[AvaloniaFact]
	public void ChangedCell_ClearsOnClickAway()
	{
		var (_, stepListBox) = ShowView();
		var row = _surface.StepColumns[0].Row;
		row.MarkChanged([RecipeTestDriver.StepDurationColumn]);
		Dispatcher.UIThread.RunJobs();

		ClickCell(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		row.IsChanged(RecipeTestDriver.StepDurationColumn).Should().BeTrue("first press only arms the pending cell");

		ClickCell(stepListBox, 0, RecipeTestDriver.CommentColumn);
		row.IsChanged(RecipeTestDriver.StepDurationColumn).Should().BeFalse("pressing any other cell clears the armed one");
	}

	[AvaloniaFact]
	public void ClickAway_ClearsChangedFlag_OnCanonicalSibling()
	{
		var canonicalSurface = _fixture.CreateCanonicalSurface();
		canonicalSurface.Initialize();
		var (_, stepListBox) = ShowView();

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);
		Dispatcher.UIThread.RunJobs();
		canonicalSurface.RecipeRows[0].IsChanged(RecipeTestDriver.TaskColumn).Should().BeTrue(
			"the action change marks seeded cells on both surfaces");

		ClickCell(stepListBox, 0, RecipeTestDriver.TaskColumn);
		ClickCell(stepListBox, 0, RecipeTestDriver.CommentColumn);

		_surface.StepColumns[0].Row.IsChanged(RecipeTestDriver.TaskColumn).Should().BeFalse();
		canonicalSurface.RecipeRows[0].IsChanged(RecipeTestDriver.TaskColumn).Should().BeFalse(
			"a click-away acknowledgement in the transposed view must clear the canonical sibling too");
	}

	[AvaloniaFact]
	public void ChangedCell_ClickAwayRunsWithoutReadOnlyGuard()
	{
		var (_, stepListBox) = ShowView();
		var row = _surface.StepColumns[0].Row;

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();
		row.MarkChanged([RecipeTestDriver.StepDurationColumn]);
		Dispatcher.UIThread.RunJobs();

		ClickCell(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		ClickCell(stepListBox, 0, RecipeTestDriver.CommentColumn);

		row.IsChanged(RecipeTestDriver.StepDurationColumn).Should().BeFalse();
	}

	[AvaloniaFact]
	public void CellClick_SelectsWholeColumn()
	{
		var (_, stepListBox) = ShowView();

		ClickCell(stepListBox, 1, RecipeTestDriver.StepDurationColumn);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndex.Should().Be(1);
		((ListBoxItem)stepListBox.ContainerFromIndex(1)!).IsSelected.Should().BeTrue();
	}

	[AvaloniaFact]
	public void PlainCellClick_SelectsColumn_WithoutFocusingEditor()
	{
		var (view, stepListBox) = ShowView();

		ClickCell(stepListBox, 1, RecipeTestDriver.StepDurationColumn);

		_surface.SelectedStepIndex.Should().Be(1);
		view.IsEditing.Should().BeFalse("a plain first click must keep Delete/Ctrl+C live");
		_window!.FocusManager!.GetFocusedElement()
			.Should().BeSameAs(stepListBox.ContainerFromIndex(1));
	}

	[AvaloniaFact]
	public void SecondClickOnSelectedColumn_FocusesEditor()
	{
		var (view, stepListBox) = ShowView();
		ClickCell(stepListBox, 1, RecipeTestDriver.StepDurationColumn);
		view.IsEditing.Should().BeFalse();

		var editor = FindTextBox(stepListBox, 1, RecipeTestDriver.StepDurationColumn);
		var clickPoint = editor.TranslatePoint(
			new Point(editor.Bounds.Width / 2, editor.Bounds.Height / 2), _window!);
		clickPoint.Should().NotBeNull();
		_window!.MouseDown(clickPoint!.Value, MouseButton.Left);
		_window!.MouseUp(clickPoint.Value, MouseButton.Left);
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeTrue("the second click on the selected column reaches the editor");
		_window!.FocusManager!.GetFocusedElement().Should().BeSameAs(editor);
	}

	[AvaloniaFact]
	public void CtrlClickOnCell_TogglesColumnMembership()
	{
		var (_, stepListBox) = ShowView();
		ClickCell(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		ClickCell(stepListBox, 2, RecipeTestDriver.StepDurationColumn, RawInputModifiers.Control);
		_surface.SelectedStepIndices.Should().Equal(0, 2);

		ClickCell(stepListBox, 2, RecipeTestDriver.StepDurationColumn, RawInputModifiers.Control);
		_surface.SelectedStepIndices.Should().Equal(0);
	}

	[AvaloniaFact]
	public void ShiftClickOnCell_ExtendsSelectionRange()
	{
		var (_, stepListBox) = ShowView();
		ClickCell(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		ClickCell(stepListBox, 2, RecipeTestDriver.StepDurationColumn, RawInputModifiers.Shift);

		_surface.SelectedStepIndices.Should().Equal(0, 1, 2);
	}

	[AvaloniaFact]
	public void RightClickOnCell_DoesNotCollapseSelection()
	{
		var (_, stepListBox) = ShowView();
		ClickCell(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		ClickCell(stepListBox, 2, RecipeTestDriver.StepDurationColumn, RawInputModifiers.Control);

		ClickCell(stepListBox, 1, RecipeTestDriver.StepDurationColumn, button: MouseButton.Right);

		_surface.SelectedStepIndices.Should().Equal(0, 2);
	}

	[AvaloniaFact]
	public void ClickOnOtherColumn_CommitsPendingEditByDefocusing()
	{
		var (_, stepListBox) = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Focus();
		editor.Text = "45";

		ClickCell(stepListBox, 1, RecipeTestDriver.StepDurationColumn);

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			45f, "taking over the press must still commit the pending edit via defocus");
	}

	[AvaloniaFact]
	public void EscapeKey_RevertsPendingText()
	{
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "100")
			.IsSuccess.Should().BeTrue();
		var (_, stepListBox) = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Focus();
		editor.Text = "0:2:5";

		_window!.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		editor.Text.Should().Be("00:01:40", "Escape must revert the typed-but-uncommitted text");
		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(100f);
	}

	// Stale-guard: a recyclable TextBox pins its edit target on focus and must write ONLY to that
	// captured cell, even after recycling rebinds it onto a different cell mid-edit. Rebinding the
	// editor's DataContext to another column's cell simulates that recycle; the pending text must
	// land on the captured cell, never leak into the rebind target.
	[AvaloniaFact]
	public void RecycledEditor_CommitsToCapturedCell_NotTheRebindTarget()
	{
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "10")
			.IsSuccess.Should().BeTrue();
		_fixture.Coordinator.UpdateStepProperty(1, RecipeTestDriver.StepDurationColumn, "20")
			.IsSuccess.Should().BeTrue();
		var (_, stepListBox) = ShowView();

		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		var capturedCell = (PropertyTextCellViewModel)editor.DataContext!;
		var rebindCell = (PropertyTextCellViewModel)FindTextBox(
			stepListBox, 1, RecipeTestDriver.StepDurationColumn).DataContext!;

		editor.Focus();
		Dispatcher.UIThread.RunJobs();

		editor.DataContext = rebindCell;
		Dispatcher.UIThread.RunJobs();
		editor.DataContext.Should().BeSameAs(rebindCell, "the recycle simulation must actually rebind the editor");

		editor.Text = "777";
		_window!.FocusManager!.Focus(null);
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns[1].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			20f, "a recycled editor must never write its pending text into the rebind-target cell");
		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			777f, "the commit must land on the cell captured when the editor was focused");
	}

	[AvaloniaFact]
	public void ReadOnlyTransition_DropsPendingEdit_AndRevertsEditorText()
	{
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "100")
			.IsSuccess.Should().BeTrue();
		var (_, stepListBox) = ShowView();
		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Focus();
		editor.Text = "45";

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			100f, "the pending edit must be dropped by the read-only guard, not committed");
		editor.Text.Should().Be(
			"00:01:40", "the editor must not keep displaying the never-committed text");
	}

	[AvaloniaFact]
	public void EditorMustClose_ClosesOpenComboBoxDropdown()
	{
		var (_, stepListBox) = ShowView();
		var comboBox = FindComboBox(stepListBox, 0, "action");
		comboBox.Focus();
		comboBox.IsDropDownOpen = true;
		Dispatcher.UIThread.RunJobs();

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		comboBox.IsDropDownOpen.Should().BeFalse();
	}

	[AvaloniaFact]
	public void StringCell_CarriesMaxLengthFromRegistry()
	{
		var (_, stepListBox) = ShowView();

		FindTextBox(stepListBox, 0, RecipeTestDriver.CommentColumn).MaxLength
			.Should().Be(_fixture.RecipeMetadataRegistry.GetStringMaxLength());
	}

	[AvaloniaFact]
	public void ClickAway_WhenArmedCellRowWasReplaced_SkipsClearWithoutThrowing()
	{
		var (_, stepListBox) = ShowView();
		var rowBefore = _surface.StepColumns[0].Row;
		rowBefore.MarkChanged([RecipeTestDriver.StepDurationColumn]);
		Dispatcher.UIThread.RunJobs();
		ClickCell(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);
		Dispatcher.UIThread.RunJobs();
		_surface.StepColumns[0].Row.Should().NotBeSameAs(rowBefore);

		var act = () => ClickCell(stepListBox, 1, RecipeTestDriver.StepDurationColumn);

		act.Should().NotThrow();
		rowBefore.IsChanged(RecipeTestDriver.StepDurationColumn).Should().BeTrue(
			"the still-in-grid guard must skip clearing a row that left the projection");
	}

	[AvaloniaFact]
	public void ActionCombo_SelectionChange_ChangesStepAction_AndMarksCells()
	{
		var (_, stepListBox) = ShowView();
		var comboBox = FindComboBox(stepListBox, 0, "action");
		var forLoopItem = _fixture.RecipeMetadataRegistry.GetActionComboBoxItems()
			.Single(item => item.Id == RecipeTestDriver.ForLoopActionId);

		comboBox.SelectedItem = forLoopItem;
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(RecipeTestDriver.ForLoopActionId);
		_surface.StepColumns[0].Row.ChangedColumns.Should().NotBeEmpty(
			"the rebuilt column's seeded cells must carry the changed highlight");
	}

	private void ClickCell(
		ListBox stepListBox,
		int columnIndex,
		string parameterKey,
		RawInputModifiers modifiers = RawInputModifiers.None,
		MouseButton button = MouseButton.Left)
	{
		var border = FindCellBorder(stepListBox, columnIndex, parameterKey);
		var clickPoint = border.TranslatePoint(new Point(3, 3), _window!);
		clickPoint.Should().NotBeNull();

		_window!.MouseDown(clickPoint!.Value, button, modifiers);
		_window!.MouseUp(clickPoint.Value, button, modifiers);
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

	private (TransposedRecipeGridView View, ListBox StepListBox) ShowView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = 1200,
			Height = 800,
			Content = view,
		};

		// Production installs the config palette at startup (App.axaml.cs). Without it the cell
		// borders have null backgrounds and are not hit-testable, so pointer tests would miss.
		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();

		return (view, stepListBox!);
	}
}
