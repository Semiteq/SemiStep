using Avalonia.Controls;

namespace SemiStep.UI.RecipeGrid;

public partial class RecipeGridHost : UserControl
{
	public RecipeGridHost()
	{
		InitializeComponent();
	}

	public IRecipeGridSurface? Surface => DataContext as IRecipeGridSurface;

	public bool IsEditing => CanonicalView.IsEditing;
}
