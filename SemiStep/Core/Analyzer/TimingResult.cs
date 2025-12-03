using Core.Loops;

namespace Core.Analyzer;

/// <summary>
/// Timing calculation outcome.
/// </summary>
public sealed record TimingResult(
	IReadOnlyDictionary<int, TimeSpan> StepStartTimes,
	TimeSpan TotalDuration,
	IReadOnlyList<LoopNode> UpdatedNodes);
