using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class GroupValueNotIntegerError(PropertyType actualType)
	: Error($"Group value must be integer, got {actualType}")
{
	public PropertyType ActualType { get; } = actualType;
}
