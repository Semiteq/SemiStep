using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class IterationCountUnsupportedTypeError(PropertyType type, int actionKey)
	: Error($"Iteration count property has unsupported type {type} in step {actionKey}")
{
	public PropertyType Type { get; } = type;

	public int ActionKey { get; } = actionKey;
}
