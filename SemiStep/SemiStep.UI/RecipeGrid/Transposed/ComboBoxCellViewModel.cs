using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class ComboBoxCellViewModel(
	RecipeRowViewModel recipeRowViewModel,
	ParameterDescriptor parameterDescriptor,
	Func<IReadOnlyList<ComboBoxItemViewModel>> itemsProvider)
	: ParameterCellViewModel(recipeRowViewModel, parameterDescriptor)
{
	public IReadOnlyList<ComboBoxItemViewModel> Items => itemsProvider();
}
