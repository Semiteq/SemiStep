using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class StringTooLongError(int length, int max, string id)
	: Error($"String length {length} exceeds maximum {max} for '{id}'")
{
	public int Length { get; } = length;

	public int Max { get; } = max;

	public string Id { get; } = id;
}
