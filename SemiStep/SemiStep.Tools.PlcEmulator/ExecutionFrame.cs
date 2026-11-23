namespace SemiStep.Tools.PlcEmulator;

/// <summary>One line shown for a number of ticks, or until the operator confirms; loop counts are 1-based.</summary>
internal sealed record ExecutionFrame(
	int Line,
	int Ticks,
	bool WaitsForOperator,
	int ForLoopCount1,
	int ForLoopCount2,
	int ForLoopCount3);
