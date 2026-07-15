using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedColumnCellsHost : Decorator
{
	private TransposedRecipeGridView? _view;
	private TransposedColumnCellsPool? _acquiredPool;
	private TransposedColumnCellsPresenter? _presenter;
	private ListBoxItem? _containerItem;
	private IDisposable? _selectionSubscription;

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
		// Pool can change on config swap; take the current pool and remember which one lent the presenter.
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

		SyncSelectionFromContainer();
	}

	// Container can be null before attach so re-resolve each bind; it's stable across in-place
	// recycle, so only resubscribe when it changes.
	private void SyncSelectionFromContainer()
	{
		if (_presenter is null)
		{
			return;
		}

		var container = this.FindAncestorOfType<ListBoxItem>();
		if (!ReferenceEquals(container, _containerItem))
		{
			_selectionSubscription?.Dispose();
			_selectionSubscription = null;
			_containerItem = container;

			if (container is not null)
			{
				// GetObservable pushes the current IsSelected synchronously on Subscribe, seeding IsColumnSelected.
				_selectionSubscription = container
					.GetObservable(ListBoxItem.IsSelectedProperty)
					.Subscribe(isSelected =>
					{
						if (_presenter is not null)
						{
							_presenter.IsColumnSelected = isSelected;
						}
					});
			}
			else
			{
				_presenter.IsColumnSelected = false;
			}

			return;
		}

		_presenter.IsColumnSelected = container?.IsSelected ?? false;
	}

	private void ReleasePresenter()
	{
		_selectionSubscription?.Dispose();
		_selectionSubscription = null;
		_containerItem = null;

		if (_presenter is null)
		{
			return;
		}

		_presenter.CommitActiveEditor();
		_presenter.IsColumnSelected = false;
		Child = null;
		_acquiredPool?.Return(_presenter);
		_presenter = null;
		_acquiredPool = null;
	}
}
