using SemiStep.Core.Shared;

namespace SemiStep.Core.Recipes.Analysis.Warnings;

public sealed class UnclosedForLoopWarning(int startIndex)
	: Warning($"Unclosed For loop starting at step {startIndex}")
{
	public int StartIndex { get; } = startIndex;
}
