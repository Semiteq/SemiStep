using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace SemiStep.UI.RecipeGrid;

/// <summary>A combo cell in either grid: text until edit, then a ComboBox whose list opens after layout.</summary>
internal sealed class LazyComboCellPresenter(
	CellEditCoordinator coordinator,
	Control display,
	Func<Control> editorBuilder,
	Func<Control, IInputElement?> focusReturnTargetOf) : LazyCellPresenter(coordinator, display, editorBuilder)
{
	private IInputElement? _focusReturnTarget;

	// Opened after layout: a list opened on the fresh, not yet laid out editor cannot focus its selected item, and
	// the list keys then stay on the ComboBox, which ignores Down and Enter while open.
	protected override void OnEnteredEdit(Control editor, string? initialText)
	{
		// Resolved on entry: an action pick replaces the row, so the cell may be detached when the list closes.
		_focusReturnTarget = focusReturnTargetOf(this);
		if (editor is ComboBox comboBox)
		{
			// The pooled editor is re-entered after exits that bypass CommitEditorContent; keep one subscription.
			comboBox.DropDownClosed -= OnDropDownClosed;
			comboBox.DropDownClosed += OnDropDownClosed;
			Dispatcher.UIThread.Post(() => OpenWhileStillEditing(comboBox), DispatcherPriority.Background);
		}
	}

	// The lock can arrive between the press and this job; an edit that may no longer open ends here.
	private void OpenWhileStillEditing(ComboBox comboBox)
	{
		if (!IsEditing || !ReferenceEquals(Editor, comboBox))
		{
			return;
		}

		if (!CanEnterEdit)
		{
			EndEditReturningFocus();
			return;
		}

		comboBox.IsDropDownOpen = true;
	}

	// Focus sat on a list item inside the popup, so the editor sees no blur when the list closes: end the edit
	// here and hand focus to the grid, where the step hotkeys act again.
	private void OnDropDownClosed(object? sender, EventArgs e)
	{
		if (!IsEditing)
		{
			return;
		}

		CommitEdit();
		ReturnFocus();
	}

	// Close the dropdown so a pooled ComboBox swapped out without a blur can't orphan its popup.
	protected override void CommitEditorContent(Control editor)
	{
		if (editor is ComboBox comboBox)
		{
			comboBox.DropDownClosed -= OnDropDownClosed;
			comboBox.IsDropDownOpen = false;
		}
	}

	// An open list keeps focus on an item in its popup, outside the editor's visual tree.
	protected override bool EditorHoldsKeyboard(Control editor)
	{
		return editor.IsKeyboardFocusWithin || editor is ComboBox { IsDropDownOpen: true };
	}

	protected override void ReturnFocus()
	{
		_focusReturnTarget?.Focus();
	}

	// The dropdown popup has its own visual root; keep editing while open so it isn't torn down.
	protected override bool ShouldExitOnEditorLostFocus(Control editor)
	{
		return editor is not ComboBox { IsDropDownOpen: true };
	}
}
