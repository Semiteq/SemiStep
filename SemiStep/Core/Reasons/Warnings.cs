namespace Core.Reasons;

internal static class Warnings
{
	internal static Warning EmptyRecipe => new("EMPTY_RECIPE");
	internal static Warning IterationCountInvalid => new("ITERATION_COUNT_INVALID");
	internal static Warning IterationCountExeeded => new("ITERATION_COUNT_EXEDED");
	internal static Warning IterationBracesUmatched => new("ITERATION_BRACES_UNMATCHED");
	internal static Warning StepDurationNegative => new("STEP_DURATION_NEGATIVE");
}
