namespace TypesShared.Plc;

public sealed record PlcExecutionInfo(
	bool RecipeActive,
	int ActualLine,
	float StepCurrentTime,
	int ForLoopCount1,
	int ForLoopCount2,
	int ForLoopCount3)
{
	public static PlcExecutionInfo Empty => new(
		RecipeActive: false,
		ActualLine: 0,
		StepCurrentTime: 0f,
		ForLoopCount1: 0,
		ForLoopCount2: 0,
		ForLoopCount3: 0);
}
