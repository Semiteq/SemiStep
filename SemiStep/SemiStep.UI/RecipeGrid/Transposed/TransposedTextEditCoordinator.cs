using Avalonia.Controls;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Owns THE ONE active lazy edit for the transposed surface across both cell kinds. Property-text and
// ComboBox cells render a lightweight display by default (TransposedLazyCellPresenter subclasses); the
// heavy TextBox / ComboBox editor is built only when this coordinator enters edit on a cell, and released
// back to the display on exit. A single active edit gives one place to reset on cell change, on blur, and
// before a pooled presenter is recycled, and one definition of "editing" for the view's exit gating.
internal sealed class TransposedTextEditCoordinator
{
	private TransposedLazyCellPresenter? _active;

	// The live editor of the current edit (a TextBox or a ComboBox), or null when no cell is being edited.
	// A focused display visual is NOT editing; only a built-and-editing presenter counts.
	public Control? ActiveEditor => _active is { IsEditing: true } presenter ? presenter.Editor : null;

	// Enters edit on a cell: swaps its display for the editor, focuses it, and applies the entry state
	// (see the subclass OnEnteredEdit). initialText is meaningful only to the text cell; the combo ignores
	// it. Focusing the new editor blurs any previous one, which self-exits.
	public void BeginEdit(TransposedLazyCellPresenter presenter, string? initialText)
	{
		if (!presenter.CanEnterEdit)
		{
			return;
		}

		if (ReferenceEquals(_active, presenter) && presenter.IsEditing)
		{
			presenter.FocusEditor();
			return;
		}

		_active = presenter;
		presenter.EnterEdit(initialText);
	}

	// Called by a presenter whose editor lost focus (blur / commit / recycle). Clears the active slot only
	// when it is still the one that ended, so a focus MOVE into a new edit (which blurs the old one after
	// _active has already advanced) does not wipe the newly active edit.
	public void NotifyEditEnded(TransposedLazyCellPresenter presenter)
	{
		if (ReferenceEquals(_active, presenter))
		{
			_active = null;
		}
	}

	// Drops the active edit reference without touching visuals; used when the view is rebound to a new
	// surface and the pooled presenters (and their editors) are discarded.
	public void Reset()
	{
		_active = null;
	}
}
