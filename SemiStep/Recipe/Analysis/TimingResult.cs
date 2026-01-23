namespace Recipe.Analysis;

public sealed record TimingResult(
	IReadOnlyDictionary<int, TimeSpan> StepStartTimes,
	TimeSpan TotalDuration);
