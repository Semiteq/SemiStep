using SemiStep.Core.Shared;

namespace SemiStep.Core.Recipes.Analysis.Warnings;

public sealed class UnmatchedEndForWarning(int stepIndex)
	: Warning($"Unmatched EndFor at step {stepIndex}")
{
	public int StepIndex { get; } = stepIndex;
}
