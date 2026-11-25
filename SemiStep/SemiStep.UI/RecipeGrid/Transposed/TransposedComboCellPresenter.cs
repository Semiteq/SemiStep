using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedComboCellPresenter(
	TransposedTextEditCoordinator coordinator,
	Control display,
	Func<Control> editorBuilder) : TransposedLazyCellPresenter(coordinator, display, editorBuilder)
{
	// Opened after layout: a list opened on the fresh, not yet laid out editor cannot focus its selected item, and
	// the list keys then stay on the ComboBox, which ignores Down and Enter while open.
	protected override void OnEnteredEdit(Control editor, string? initialText)
	{
		if (editor is ComboBox comboBox)
		{
			// The pooled editor is re-entered after exits that bypass CommitEditorContent; keep one subscription.
			comboBox.DropDownClosed -= OnDropDownClosed;
			comboBox.DropDownClosed += OnDropDownClosed;
			Dispatcher.Post(() => OpenWhileStillEditing(comboBox), DispatcherPriority.Background);
		}
	}

	private void OpenWhileStillEditing(ComboBox comboBox)
	{
		if (IsEditing && ReferenceEquals(Editor, comboBox))
		{
			comboBox.IsDropDownOpen = true;
		}
	}

	// Focus sat on a list item inside the popup, so the editor sees no blur when the list closes: end the edit
	// here and hand focus to the column, where the step hotkeys act again.
	private void OnDropDownClosed(object? sender, EventArgs e)
	{
		if (!IsEditing)
		{
			return;
		}

		var column = this.FindAncestorOfType<ListBoxItem>();
		CommitEdit();
		column?.Focus();
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

	// The dropdown popup has its own visual root; keep editing while open so it isn't torn down.
	protected override bool ShouldExitOnEditorLostFocus(Control editor)
	{
		return editor is not ComboBox { IsDropDownOpen: true };
	}
}
