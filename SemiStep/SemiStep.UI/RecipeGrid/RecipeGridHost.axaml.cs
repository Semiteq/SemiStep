using Avalonia;
using Avalonia.Controls;

using ReactiveUI;

using SemiStep.Core.Configuration;

using SemiStep.UI.RecipeGrid.Transposed;

namespace SemiStep.UI.RecipeGrid;

public partial class RecipeGridHost : UserControl
{
	private readonly CanonicalRecipeGridView _canonicalView = new();
	private readonly TransposedRecipeGridView _transposedView = new();

	private IDisposable? _orientationSubscription;

	public RecipeGridHost()
	{
		InitializeComponent();
	}

	public IRecipeGridSurface? Surface => DataContext as IRecipeGridSurface;

	public bool IsEditing => Content switch
	{
		CanonicalRecipeGridView view => view.IsEditing,
		TransposedRecipeGridView view => view.IsEditing,
		_ => false
	};

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);

		WireRouter();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		if (_orientationSubscription is null)
		{
			WireRouter();
		}
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnDetachedFromVisualTree(e);

		_orientationSubscription?.Dispose();
		_orientationSubscription = null;
	}

	private void WireRouter()
	{
		_orientationSubscription?.Dispose();
		_orientationSubscription = null;

		if (DataContext is not ActiveRecipeGridSurface router)
		{
			_canonicalView.DataContext = null;
			_transposedView.DataContext = null;
			Content = null;

			return;
		}

		// Each child view's DataContext is set explicitly to its concrete surface: letting the
		// views inherit the router would silently null out ReactiveUserControl<T>.ViewModel,
		// because the router is not assignable to either concrete surface type.
		_canonicalView.DataContext = router.CanonicalSurface;
		_transposedView.DataContext = router.TransposedSurface;

		_orientationSubscription = router
			.WhenAnyValue(x => x.Orientation)
			.Subscribe(ApplyOrientation);
	}

	// The incoming view is kept alive across flips and its selection control still holds the
	// visual selection from before it was flipped away; re-apply the surface's carried-over
	// selection so the highlight matches what Delete/Ctrl+C will act on.
	private void ApplyOrientation(GridOrientation orientation)
	{
		if (orientation == GridOrientation.ColumnsAsSteps)
		{
			Content = _transposedView;
			_transposedView.SyncSelectionFromSurface();
		}
		else
		{
			Content = _canonicalView;
			_canonicalView.SyncSelectionFromSurface();
		}
	}
}
