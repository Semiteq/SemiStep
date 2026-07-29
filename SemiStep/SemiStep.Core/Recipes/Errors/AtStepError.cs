using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class AtStepError(int stepNumber, IError inner)
	: Error($"Step {stepNumber}: {inner.Message}")
{
	public int StepNumber { get; } = stepNumber;

	public IError Inner { get; } = inner;
}
