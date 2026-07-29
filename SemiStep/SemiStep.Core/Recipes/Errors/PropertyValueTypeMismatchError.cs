using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class PropertyValueTypeMismatchError(string expectedType, string actualType, string id)
	: Error($"Expected {expectedType} value but got {actualType} for '{id}'")
{
	public string ExpectedType { get; } = expectedType;

	public string ActualType { get; } = actualType;

	public string Id { get; } = id;
}
