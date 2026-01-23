namespace Recipe.Analysis;

public sealed record AnalysisError(int? StepIndex, string Message);

public sealed record AnalysisWarning(int? StepIndex, string Message);

public sealed record AnalysisResult(
	bool IsValid,
	LoopStructure Loops,
	TimingResult Timing,
	IReadOnlyList<AnalysisError> Errors,
	IReadOnlyList<AnalysisWarning> Warnings)
{
	public static AnalysisResult Success(LoopStructure loops, TimingResult timing)
		=> new(true, loops, timing, [], []);

	public static AnalysisResult WithWarnings(LoopStructure loops, TimingResult timing, IEnumerable<AnalysisWarning> warnings)
		=> new(true, loops, timing, [], warnings.ToList());

	public static AnalysisResult Failure(LoopStructure loops, TimingResult timing, IEnumerable<AnalysisError> errors, IEnumerable<AnalysisWarning>? warnings = null)
		=> new(false, loops, timing, errors.ToList(), warnings?.ToList() ?? []);

	public static AnalysisResult Empty => new(
		true,
		LoopStructure.Empty,
		new TimingResult(new Dictionary<int, TimeSpan>(), TimeSpan.Zero),
		[],
		[]);
}
