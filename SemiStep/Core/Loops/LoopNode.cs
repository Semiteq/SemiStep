namespace Core.Loops;

/// <summary>
/// Represents a single loop construct (For ... EndFor).
/// EndIndex and BodyEndIndex are null for incomplete loops.
/// SingleIterationDuration may be null until timing calculation phase completes.
/// </summary>
public sealed record LoopNode(
	int StartIndex,
	int? EndIndex,
	int NestingDepth,
	int IterationCountRaw,
	int EffectiveIterationCount,
	int BodyStartIndex,
	int? BodyEndIndex,
	TimeSpan? SingleIterationDuration,
	LoopStatus Status);
