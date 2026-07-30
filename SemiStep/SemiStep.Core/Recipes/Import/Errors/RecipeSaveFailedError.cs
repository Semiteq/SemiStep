using FluentResults;

namespace SemiStep.Core.Recipes.Import.Errors;

public sealed class RecipeSaveFailedError(string filePath)
	: Error($"Failed to save recipe to '{filePath}'")
{
	public string FilePath { get; } = filePath;
}
