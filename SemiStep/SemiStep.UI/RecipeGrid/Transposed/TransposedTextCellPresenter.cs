using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedTextCellPresenter : LazyCellPresenter
{
	public TransposedTextCellPresenter(CellEditCoordinator coordinator, Control display, Func<Control> editorBuilder)
		: base(coordinator, display, editorBuilder)
	{
		Focusable = true;
		AddHandler(KeyDownEvent, OnDisplayKeyDown, RoutingStrategies.Bubble);
		AddHandler(TextInputEvent, OnDisplayTextInput, RoutingStrategies.Bubble);
	}

	// Applies the initial text (see the coordinator's BeginEdit for the null / "" / seed contract): null
	// selects the whole value (F2 / pointer entry) so typing replaces it; "" clears it so the paired
	// TextInput of a printable keystroke types fresh; a non-empty value seeds it directly.
	protected override void OnEnteredEdit(Control editor, string? initialText)
	{
		if (editor is not TextBox textBox)
		{
			return;
		}

		if (initialText is null)
		{
			textBox.SelectAll();
			return;
		}

		textBox.Text = initialText;
		textBox.CaretIndex = initialText.Length;
	}

	protected override void CommitEditorContent(Control editor)
	{
		if (editor is TextBox textBox)
		{
			TransposedTextEditorFactory.CommitEditor(textBox);
		}
	}

	// Seed '' so the paired TextInput types fresh (replace semantics). The KeyDown stays unhandled: Avalonia.Win32
	// drops the WM_CHAR of a handled KeyDown, and that TextInput carries the character to the focused editor.
	private void OnDisplayKeyDown(object? sender, KeyEventArgs e)
	{
		if (IsEditing || !IsEffectivelyEnabled || !CellEditKeys.IsPrintable(e.KeySymbol, e.KeyModifiers))
		{
			return;
		}

		BeginEdit(initialText: string.Empty);
	}

	// A TextInput that reaches the display without a preceding printable KeyDown carries the only copy of
	// the character, so seed the editor with it directly.
	private void OnDisplayTextInput(object? sender, TextInputEventArgs e)
	{
		if (IsEditing || !IsEffectivelyEnabled || !CellEditKeys.IsPrintable(e.Text, KeyModifiers.None))
		{
			return;
		}

		BeginEdit(initialText: e.Text);
		e.Handled = true;
	}
}
