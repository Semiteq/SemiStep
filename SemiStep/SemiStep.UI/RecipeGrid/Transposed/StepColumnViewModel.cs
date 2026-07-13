using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class StepColumnViewModel : IDisposable
{
	public StepColumnViewModel(
		int stepNumber,
		Step step,
		ActionDefinition action,
		RecipeMetadataRegistry recipeMetadataRegistry,
		IReadOnlyList<ParameterDescriptor> parameterDescriptors,
		Func<RecipeRowViewModel, ParameterDescriptor, ParameterCellViewModel> cellFactory)
	{
		var inapplicableColumns = RecipeRowViewModel.BuildInapplicableColumns(action, step, recipeMetadataRegistry);
		Row = new RecipeRowViewModel(stepNumber, step, action, recipeMetadataRegistry, inapplicableColumns);
		Cells = parameterDescriptors.Select(descriptor => cellFactory(Row, descriptor)).ToList();
	}

	public RecipeRowViewModel Row { get; }

	public IReadOnlyList<ParameterCellViewModel> Cells { get; }

	public void Dispose()
	{
		foreach (var cell in Cells)
		{
			cell.Dispose();
		}

		Row.Dispose();
	}
}
