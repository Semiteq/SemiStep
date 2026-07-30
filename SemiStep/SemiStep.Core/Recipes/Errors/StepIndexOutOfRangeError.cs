using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class StepIndexOutOfRangeError(int index, int stepCount)
	: Error($"Step index {index} is out of range for recipe with {stepCount} steps")
{
	public int Index { get; } = index;

	public int StepCount { get; } = stepCount;
}
