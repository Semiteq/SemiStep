using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class StringContainsNulError(string id)
	: Error($"String value contains embedded NUL character for '{id}'")
{
	public string Id { get; } = id;
}
