namespace TypesShared.Plc;

public sealed record PlcManagingAreaState(
	bool Committed,
	int RecipeLines);
