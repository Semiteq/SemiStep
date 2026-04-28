namespace TypesShared.Core;

public sealed record LoopInfo(
	int StartIndex,
	int EndIndex,
	int Depth,
	int Iterations);
