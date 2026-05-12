namespace SemiStep.Core.Plc.State;

public sealed record PlcManagingAreaState(
	bool Committed,
	int RecipeLines);
