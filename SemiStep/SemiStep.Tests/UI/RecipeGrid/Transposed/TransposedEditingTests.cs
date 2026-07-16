using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
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
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

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

		DisplayText(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Should().Be("00:01:40");

		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Text = "0:2:5";
		_window!.FocusManager!.Focus(null);
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(125f);
		DisplayText(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Should().Be("00:02:05");
	}

	[AvaloniaFact]
	public void InvalidTimeInput_StaysUncommitted()
	{
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "100")
			.IsSuccess.Should().BeTrue();

		var (_, stepListBox) = ShowView();
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		editor.Text = "99:99:99";
		_window!.FocusManager!.Focus(null);
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(100f);
		DisplayText(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Should().Be(
			"00:01:40", "a rejected edit must snap the display back to the model's formatted value");
	}

	[AvaloniaFact]
	public void InapplicableCell_EditorIsDisabled()
	{
		var (_, stepListBox) = ShowView();

		FindTextPresenter(stepListBox, 0, RecipeTestDriver.TaskColumn).IsEnabled.Should().BeFalse();
		FindTextPresenter(stepListBox, 0, RecipeTestDriver.StepDurationColumn).IsEnabled.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ReadOnlyMode_DisablesEditors_AndEditorMustCloseDefocusesActiveOne()
	{
		var (view, stepListBox) = ShowView();
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		view.IsEditing.Should().BeTrue();

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		FindTextPresenter(stepListBox, 0, RecipeTestDriver.StepDurationColumn).IsEnabled.Should().BeFalse();
		FindComboPresenter(stepListBox, 0, "action").IsEnabled.Should().BeFalse();
		view.IsEditing.Should().BeFalse();
		_window!.FocusManager!.GetFocusedElement().Should().NotBeSameAs(editor);

		_fixture.SetRecipeActive(false);
		Dispatcher.UIThread.RunJobs();

		FindTextPresenter(stepListBox, 0, RecipeTestDriver.StepDurationColumn).IsEnabled.Should().BeTrue();
	}

	[AvaloniaFact]
	public void IsEditing_TracksEditorFocus()
	{
		var (view, stepListBox) = ShowView();

		view.IsEditing.Should().BeFalse();

		EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		view.IsEditing.Should().BeTrue();

		_window!.FocusManager!.Focus(null);
		Dispatcher.UIThread.RunJobs();
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

		ClickCell(stepListBox, 1, RecipeTestDriver.StepDurationColumn);

		view.IsEditing.Should().BeTrue("the second click on the selected column enters edit");
		var editor = FindTextBox(stepListBox, 1, RecipeTestDriver.StepDurationColumn);
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
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
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
		var (view, stepListBox) = ShowView();
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Text = "0:2:5";

		_window!.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		// Escape reverts the typed-but-uncommitted text and exits edit (canonical parity), so the value is
		// held by the display, not the discarded editor, and the model is untouched.
		view.IsEditing.Should().BeFalse("Escape cancels the cell edit");
		DisplayText(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Should().Be(
			"00:01:40", "Escape must revert the display to the model's formatted value");
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

		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		var capturedCell = (PropertyTextCellViewModel)editor.DataContext!;
		var rebindCell = (PropertyTextCellViewModel)FindTextPresenter(
			stepListBox, 1, RecipeTestDriver.StepDurationColumn).DataContext!;

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
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Text = "45";

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		_surface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn].Should().Be(
			100f, "the pending edit must be dropped by the read-only guard, not committed");
		DisplayText(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Should().Be(
			"00:01:40", "the display must not keep showing the never-committed text");
	}

	[AvaloniaFact]
	public void EditorMustClose_ClosesOpenComboBoxDropdown()
	{
		var (_, stepListBox) = ShowView();
		var comboBox = EnterComboEdit(stepListBox, 0, "action");
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

		EnterTextEdit(stepListBox, 0, RecipeTestDriver.CommentColumn).MaxLength
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
		var comboBox = EnterComboEdit(stepListBox, 0, "action");
		var forLoopItem = _fixture.RecipeMetadataRegistry.GetActionComboBoxItems()
			.Single(item => item.Id == RecipeTestDriver.ForLoopActionId);

		comboBox.SelectedItem = forLoopItem;
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(RecipeTestDriver.ForLoopActionId);
		_surface.StepColumns[0].Row.ChangedColumns.Should().NotBeEmpty(
			"the rebuilt column's seeded cells must carry the changed highlight");
	}

	// First-keystroke fidelity, KeyDown route: a printable KeyDown on a focused display presenter enters
	// edit and clears the cell; the paired TextInput (a separate raw event) lands on the now-focused editor.
	// Exactly one character survives — neither dropped by the swap nor doubled by both handlers firing.
	[AvaloniaFact]
	public void PrintableKeyThenTextInput_EntersEdit_KeepsExactlyOneCharacter()
	{
		var (view, stepListBox) = ShowView();
		FindTextPresenter(stepListBox, 0, RecipeTestDriver.CommentColumn).Focus();

		_window!.KeyPressQwerty(PhysicalKey.Digit5, RawInputModifiers.None);
		_window!.KeyTextInput("5");
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeTrue("a printable keystroke on a focused display enters edit");
		FindTextBox(stepListBox, 0, RecipeTestDriver.CommentColumn).Text
			.Should().Be("5", "the first character is typed exactly once");
	}

	// First-keystroke fidelity, pure TextInput route (IME / paste-like, no preceding printable KeyDown):
	// the display seeds the editor directly with the character it carries.
	[AvaloniaFact]
	public void TextInputWithoutKeyDown_EntersEdit_KeepsExactlyOneCharacter()
	{
		var (view, stepListBox) = ShowView();
		FindTextPresenter(stepListBox, 0, RecipeTestDriver.CommentColumn).Focus();

		_window!.KeyTextInput("7");
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeTrue();
		FindTextBox(stepListBox, 0, RecipeTestDriver.CommentColumn).Text.Should().Be("7");
	}

	// A rapid two-character burst over the KeyDown route must land both characters on the editor in order,
	// with no drop and no double.
	[AvaloniaFact]
	public void RapidTwoCharacterBurst_TypesBothInOrder()
	{
		var (_, stepListBox) = ShowView();
		FindTextPresenter(stepListBox, 0, RecipeTestDriver.CommentColumn).Focus();

		_window!.KeyPressQwerty(PhysicalKey.Digit5, RawInputModifiers.None);
		_window!.KeyTextInput("5");
		_window!.KeyPressQwerty(PhysicalKey.Digit7, RawInputModifiers.None);
		_window!.KeyTextInput("7");
		Dispatcher.UIThread.RunJobs();

		FindTextBox(stepListBox, 0, RecipeTestDriver.CommentColumn).Text
			.Should().Be("57", "both characters land on the editor in order with no drop or double");
	}

	// Keyboard traversal focuses the display presenter (its editor is not built); F2 on that focused display
	// is what opens the editor.
	[AvaloniaFact]
	public void KeyboardTraversal_FocusesDisplayPresenter_ThenF2EntersEdit()
	{
		var (view, stepListBox) = ShowView();
		FindComboPresenter(stepListBox, 0, "action").Focus();

		_window!.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		var presenter = FindTextPresenter(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		_window!.FocusManager!.GetFocusedElement().Should().BeSameAs(
			presenter, "arrow traversal focuses the display presenter, not a built editor");
		view.IsEditing.Should().BeFalse("a focused display is not an active edit");
		presenter.GetVisualDescendants().OfType<TextBox>()
			.Should().BeEmpty("no editor exists until edit entry");

		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeTrue("F2 on the focused display opens the editor");
	}

	// An inapplicable text cell is non-focusable and blocks edit entry: F2 must not build an editor.
	[AvaloniaFact]
	public void InapplicableTextCell_DoesNotEnterEdit()
	{
		var (view, stepListBox) = ShowView();
		var presenter = FindTextPresenter(stepListBox, 0, RecipeTestDriver.TaskColumn);
		presenter.IsEnabled.Should().BeFalse("the task cell is inapplicable for a wait step");

		presenter.Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeFalse("an inapplicable cell must not enter edit");
		presenter.GetVisualDescendants().OfType<TextBox>()
			.Should().BeEmpty("no editor is built for a blocked cell");
	}

	// A read-only surface (PLC sync) blocks text edit entry: the presenter is disabled and F2 does nothing.
	[AvaloniaFact]
	public void SurfaceReadOnly_TextCell_DoesNotEnterEdit()
	{
		var (view, stepListBox) = ShowView();
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		var presenter = FindTextPresenter(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		presenter.IsEnabled.Should().BeFalse("a read-only surface disables text cells");

		presenter.Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeFalse("a read-only surface must block text edit entry");
	}

	// Regression: a press inside an already-open text editor must reach the live TextBox so the caret can be
	// repositioned. The tunnel entry handler consumes only the entry press (a not-yet-editing display); once
	// editing, it must leave the press unhandled so the TextBox receives it (a handled tunnel press never
	// reaches the editor).
	[AvaloniaFact]
	public void ClickInsideOpenEditor_ReachesLiveTextBox_ForCaretReposition()
	{
		var (view, stepListBox) = ShowView();

		// Enter edit via the production gesture (first click selects the column, second click opens the
		// editor) so the column is selected AND editing — the state in which a further press must reach the
		// live TextBox to reposition the caret rather than being swallowed by the tunnel entry handler.
		ClickCell(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		ClickCell(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		view.IsEditing.Should().BeTrue("the second click opens the editor");

		var editor = FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		var reachedEditor = false;
		editor.AddHandler(
			InputElement.PointerPressedEvent,
			(_, _) => reachedEditor = true,
			RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

		ClickCell(stepListBox, 0, RecipeTestDriver.StepDurationColumn);

		reachedEditor.Should().BeTrue(
			"a press inside the open editor must not be swallowed by the tunnel entry handler");
		view.IsEditing.Should().BeTrue("clicking inside the open editor keeps it in edit");
	}

	// First-keystroke replace semantics on a NON-empty cell: a printable KeyDown on a focused display enters
	// edit and clears the seeded value, so the paired TextInput types fresh (replace, not append).
	[AvaloniaFact]
	public void FirstPrintableKey_OnNonEmptyCell_ReplacesValue()
	{
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "100")
			.IsSuccess.Should().BeTrue();
		var (view, stepListBox) = ShowView();
		DisplayText(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Should().Be(
			"00:01:40", "precondition: the target cell holds a non-empty value");

		FindTextPresenter(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Focus();
		_window!.KeyPressQwerty(PhysicalKey.Digit5, RawInputModifiers.None);
		_window!.KeyTextInput("5");
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeTrue("a printable keystroke on a focused display enters edit");
		FindTextBox(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Text.Should().Be(
			"5", "the first printable keystroke replaces the seeded value, it does not append to it");
	}

	// After an Enter commit the editor is released back to the display within the still-realized container:
	// zero live TextBox remains and the display TextBlock shows the committed value.
	[AvaloniaFact]
	public void EnterCommit_ReleasesEditorToDisplay_WithinRealizedContainer()
	{
		var (view, stepListBox) = ShowView();
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		var editor = EnterTextEdit(stepListBox, 0, RecipeTestDriver.StepDurationColumn);
		editor.Text = "45";

		_window!.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		view.IsEditing.Should().BeFalse("Enter commits and ends the edit");
		container.GetVisualDescendants().OfType<TextBox>().Should().BeEmpty(
			"the committed editor is released back to the display, leaving no live TextBox in the container");
		DisplayText(stepListBox, 0, RecipeTestDriver.StepDurationColumn).Should().Be(
			"00:00:45", "the restored display shows the committed value");
	}

	// A rapid two-character burst over the pure TextInput route (no preceding printable KeyDown): the first
	// TextInput seeds the editor via OnDisplayTextInputCore, the second lands on the now-focused editor.
	[AvaloniaFact]
	public void RapidTwoCharacterBurst_PureTextInputRoute_TypesBothInOrder()
	{
		var (_, stepListBox) = ShowView();
		FindTextPresenter(stepListBox, 0, RecipeTestDriver.CommentColumn).Focus();

		_window!.KeyTextInput("5");
		_window!.KeyTextInput("7");
		Dispatcher.UIThread.RunJobs();

		FindTextBox(stepListBox, 0, RecipeTestDriver.CommentColumn).Text.Should().Be(
			"57", "the pure-TextInput route seeds the first char, then the focused editor takes the second");
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

	private static TransposedTextCellPresenter FindTextPresenter(
		ListBox stepListBox, int columnIndex, string parameterKey)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;

		return container.GetVisualDescendants()
			.OfType<TransposedTextCellPresenter>()
			.Single(presenter => presenter.DataContext is ParameterCellViewModel cell
				&& cell.Descriptor.ParameterKey == parameterKey);
	}

	private static string? DisplayText(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		return FindTextPresenter(stepListBox, columnIndex, parameterKey)
			.GetVisualDescendants()
			.OfType<TextBlock>()
			.First()
			.Text;
	}

	// Enters edit on a lazy property-text cell through the real focus + F2 gesture, then returns the
	// now-built editor. The editor exists only while editing, so tests grab it through this.
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

	// Enters edit on a lazy combo cell through the real focus + F2 gesture (which opens the dropdown), then
	// returns the now-built ComboBox. The ComboBox exists only while editing, so tests grab it through this.
	private ComboBox EnterComboEdit(ListBox stepListBox, int columnIndex, string parameterKey)
	{
		FindComboPresenter(stepListBox, columnIndex, parameterKey).Focus();
		_window!.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		return FindComboBox(stepListBox, columnIndex, parameterKey);
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

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox!.UseTransposedColumnsPanel();

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return (view, stepListBox);
	}
}
