using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Lightweight seam between the recycled ListBox container and a pooled column-cells presenter. It sits
// in the ItemTemplate (so it IS rebuilt on every recycle, but it carries no cell subtree of its own), and
// on realize it borrows a presenter from the view's pool, binds it to the column, and hosts it; on
// recycle-out it commits any active edit and returns the presenter. This keeps the heavy cell subtrees
// out of the rebuilt-on-recycle content while the ListBox still virtualizes the columns.
internal sealed class TransposedColumnCellsHost : Decorator
{
	private TransposedRecipeGridView? _view;
	private TransposedColumnCellsPool? _acquiredPool;
	private TransposedColumnCellsPresenter? _presenter;

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		_view ??= this.FindAncestorOfType<TransposedRecipeGridView>();
		AcquireAndBind();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		ReleasePresenter();
		base.OnDetachedFromVisualTree(e);
	}

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		AcquireAndBind();
	}

	private void AcquireAndBind()
	{
		// The surface (and its pool) can change under the singleton view on a config swap; always take
		// the current pool and remember which one lent the presenter so it is returned to its origin.
		if (_view?.ColumnCellsPool is not { } pool || DataContext is not StepColumnViewModel column)
		{
			return;
		}

		if (_presenter is null)
		{
			_presenter = pool.Acquire();
			_acquiredPool = pool;
		}

		_presenter.BindColumn(column);

		if (!ReferenceEquals(Child, _presenter))
		{
			Child = _presenter;
		}
	}

	private void ReleasePresenter()
	{
		if (_presenter is null)
		{
			return;
		}

		_presenter.CommitActiveEditor();
		Child = null;
		_acquiredPool?.Return(_presenter);
		_presenter = null;
		_acquiredPool = null;
	}
}
