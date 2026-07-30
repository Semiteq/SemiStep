using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class MaxLoopNestingDepthExceededError(int maxAllowed, int actualDepth)
	: Error($"Maximum loop nesting depth ({maxAllowed}) exceeded: {actualDepth}")
{
	public int MaxAllowed { get; } = maxAllowed;

	public int ActualDepth { get; } = actualDepth;
}
