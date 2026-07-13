using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// String-typed columns cap the editor at the PLC string block length; every other type has
/// no editor length limit and gets no entry (consumers read via GetValueOrDefault).
/// </summary>
internal static class StringColumnMaxLengths
{
	public static IReadOnlyDictionary<string, int?> Build(RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var result = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
		foreach (var column in recipeMetadataRegistry.GetAllColumns())
		{
			var propertyResult = recipeMetadataRegistry.GetProperty(column.PropertyTypeId);
			var isStringTyped = propertyResult.IsSuccess
				&& SystemTypes.Comparer.Equals(propertyResult.Value.SystemType, SystemTypes.String);

			if (isStringTyped)
			{
				result[column.Key] = recipeMetadataRegistry.GetStringMaxLength();
			}
		}

		return result;
	}
}
