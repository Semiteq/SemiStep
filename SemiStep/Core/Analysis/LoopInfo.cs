namespace Core.Analysis;

public sealed record LoopInfo(
	int StartIndex,
	int? EndIndex,
	int NestingDepth,
	int IterationCount,
	TimeSpan? SingleIterationDuration,
	LoopStatus Status);
