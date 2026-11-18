using SemiStep.Core.Shared;

namespace SemiStep.Core.Recipes.Analysis.Warnings;

public sealed class UnclosedForLoopWarning(int stepNumber)
	: Warning($"Unclosed For loop starting at step {stepNumber}")
{
	public int StepNumber { get; } = stepNumber;
}
