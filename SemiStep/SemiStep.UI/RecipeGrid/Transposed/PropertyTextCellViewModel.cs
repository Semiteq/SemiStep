namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class PropertyTextCellViewModel(
	RecipeRowViewModel recipeRowViewModel,
	ParameterDescriptor parameterDescriptor,
	int? maxLength)
	: ParameterCellViewModel(recipeRowViewModel, parameterDescriptor)
{
	public int? MaxLength { get; } = maxLength;
}
