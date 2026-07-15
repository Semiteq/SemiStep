using Avalonia.Controls;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedComboCellPresenter : TransposedLazyCellPresenter
{
	public TransposedComboCellPresenter(
		TransposedTextEditCoordinator coordinator,
		Control display,
		Func<Control> editorBuilder)
		: base(coordinator, display, editorBuilder)
	{
	}

	protected override void OnEnteredEdit(Control editor, string? initialText)
	{
		if (editor is ComboBox comboBox)
		{
			comboBox.IsDropDownOpen = true;
		}
	}

	// Close the dropdown so a pooled ComboBox swapped out without a blur can't orphan its popup.
	protected override void CommitEditorContent(Control editor)
	{
		if (editor is ComboBox comboBox)
		{
			comboBox.IsDropDownOpen = false;
		}
	}

	// The dropdown popup has its own visual root; keep editing while open so it isn't torn down.
	protected override bool ShouldExitOnEditorLostFocus(Control editor)
	{
		return editor is not ComboBox { IsDropDownOpen: true };
	}
}
