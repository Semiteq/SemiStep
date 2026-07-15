using Avalonia.Controls;
using Avalonia.Input;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedTextCellPresenter : TransposedLazyCellPresenter
{
	public TransposedTextCellPresenter(
		TransposedTextEditCoordinator coordinator,
		Control display,
		Func<Control> editorBuilder)
		: base(coordinator, display, editorBuilder)
	{
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
			TransposedCellTemplateFactory.CommitEditor(textBox);
		}
	}

	// Seed '' so the paired TextInput types fresh (replace semantics); char neither dropped nor doubled.
	protected override void OnPrintableKeyDown(KeyEventArgs e)
	{
		if (IsPrintable(e.KeySymbol, e.KeyModifiers))
		{
			BeginEdit(initialText: string.Empty);
			e.Handled = true;
		}
	}

	// A TextInput that reaches the display without a preceding printable KeyDown carries the only copy of
	// the character, so seed the editor with it directly.
	protected override void OnDisplayTextInputCore(TextInputEventArgs e)
	{
		if (!IsPrintable(e.Text, KeyModifiers.None))
		{
			return;
		}

		BeginEdit(initialText: e.Text);
		e.Handled = true;
	}
}
