using SemiStep.Core.Shared;

namespace SemiStep.Core.Recipes.Analysis.Warnings;

public sealed class UnmatchedEndForWarning(int stepNumber)
	: Warning($"Unmatched EndFor at step {stepNumber}")
{
	public int StepNumber { get; } = stepNumber;
}
