using FluentResults;

namespace SemiStep.Core.Recipes.Import.Errors;

public sealed class RecipeFileNotFoundError(string filePath)
	: Error($"Recipe file not found: {filePath}")
{
	public string FilePath { get; } = filePath;
}
