namespace TypesShared.Plc.Memory;

public sealed record ExecutionDbLayout(
	int DbNumber,
	int RecipeActiveOffset,
	int ActualLineOffset,
	int StepCurrentTimeOffset,
	int ForLoopCount1Offset,
	int ForLoopCount2Offset,
	int ForLoopCount3Offset,
	int TotalSize)
{
	public static ExecutionDbLayout Default => new(
		DbNumber: 6,
		RecipeActiveOffset: 0,
		ActualLineOffset: 2,
		StepCurrentTimeOffset: 6,
		ForLoopCount1Offset: 10,
		ForLoopCount2Offset: 14,
		ForLoopCount3Offset: 18,
		TotalSize: 22);
}
