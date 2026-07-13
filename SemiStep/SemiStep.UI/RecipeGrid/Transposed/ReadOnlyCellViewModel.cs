namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class ReadOnlyCellViewModel(
	RecipeRowViewModel recipeRowViewModel,
	ParameterDescriptor parameterDescriptor)
	: ParameterCellViewModel(recipeRowViewModel, parameterDescriptor)
{
	protected override void WriteValue(object? value)
	{
	}
}
