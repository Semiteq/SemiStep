using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class ColumnNotFoundError(string key)
	: Error($"Column '{key}' not found")
{
	public string Key { get; } = key;
}
