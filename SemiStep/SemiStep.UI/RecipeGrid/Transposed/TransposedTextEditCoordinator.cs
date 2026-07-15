using Avalonia.Controls;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedTextEditCoordinator
{
	private TransposedLazyCellPresenter? _active;

	// A focused display is NOT editing; only a built-and-editing presenter counts.
	public Control? ActiveEditor => _active is { IsEditing: true } presenter ? presenter.Editor : null;

	// Focusing the new editor blurs any previous one, which self-exits.
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

	// Clear only when still the active slot, so a focus MOVE into a new edit doesn't wipe it.
	public void NotifyEditEnded(TransposedLazyCellPresenter presenter)
	{
		if (ReferenceEquals(_active, presenter))
		{
			_active = null;
		}
	}

	// Drops the active edit without touching visuals (pooled presenters are discarded on surface rebind).
	public void Reset()
	{
		_active = null;
	}
}
