namespace SemiStep.Core.Plc.S7.Protocol;

internal sealed record ManagingAreaPcData(
	bool Committed,
	int RecipeLines);
