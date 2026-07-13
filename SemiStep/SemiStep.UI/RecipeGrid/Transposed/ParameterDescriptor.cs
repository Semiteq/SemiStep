using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed record ParameterDescriptor(
	string ParameterKey,
	string ParameterDisplayName,
	string ColumnType,
	bool IsReadOnlyParameter)
{
	public static IReadOnlyList<ParameterDescriptor> BuildFromRegistry(RecipeMetadataRegistry recipeMetadataRegistry)
	{
		return recipeMetadataRegistry
			.GetAllColumns()
			.Select(column => new ParameterDescriptor(column.Key, column.UiName, column.ColumnType, column.ReadOnly))
			.ToList();
	}
}
