namespace Core.Analysis;

public sealed record LoopInfo(
	int StartIndex,
	int EndIndex,
	int Depth,
	int Iterations);
