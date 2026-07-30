using FluentResults;

namespace SemiStep.Core.Recipes.Import.Errors;

public sealed class RecipeLoadFailedError(string filePath)
	: Error($"Failed to load recipe from '{filePath}'")
{
	public string FilePath { get; } = filePath;
}
