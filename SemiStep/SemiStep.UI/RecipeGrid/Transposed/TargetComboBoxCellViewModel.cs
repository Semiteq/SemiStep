using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class TargetComboBoxCellViewModel(
	RecipeRowViewModel recipeRowViewModel,
	ParameterDescriptor parameterDescriptor)
	: ParameterCellViewModel(recipeRowViewModel, parameterDescriptor)
{
	public IReadOnlyList<ComboBoxItemViewModel> Items => Row.GroupItemsByColumn[Descriptor.ParameterKey];
}
