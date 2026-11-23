namespace SemiStep.Tools.PlcEmulator;

internal enum RunRefusalReason
{
	NoCommittedRecipe,
	RecipeDoesNotDecode,
	RecipeHasNoLines,
	RecipeFailsAnalysis,
	AlreadyRunning,
	NotRunning,
	NotPaused
}
