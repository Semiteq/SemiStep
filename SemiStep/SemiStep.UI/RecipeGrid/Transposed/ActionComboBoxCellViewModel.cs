using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class ActionComboBoxCellViewModel(
	RecipeRowViewModel recipeRowViewModel,
	ParameterDescriptor parameterDescriptor,
	IReadOnlyList<ComboBoxItemViewModel> actionItems)
	: ParameterCellViewModel(recipeRowViewModel, parameterDescriptor)
{
	public IReadOnlyList<ComboBoxItemViewModel> Items { get; } = actionItems;
}
