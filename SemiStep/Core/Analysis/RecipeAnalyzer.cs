using Core.Entities;

namespace Core.Analysis;

public sealed class RecipeAnalyzer
{
	private readonly LoopParser _loopParser;
	private readonly TimingCalculator _timingCalculator;

	private const int MaxLoopDepth = 10;

	public RecipeAnalyzer(LoopParser loopParser, TimingCalculator timingCalculator)
	{
		_loopParser = loopParser;
		_timingCalculator = timingCalculator;
	}

	public AnalysisResult Analyze(Recipe recipe)
	{
		if (recipe.Steps.Count == 0)
		{
			return AnalysisResult.WithWarnings(
				LoopStructure.Empty,
				new TimingResult(new Dictionary<int, TimeSpan>(), TimeSpan.Zero),
				[new AnalysisWarning(null, "Recipe is empty")]);
		}

		var loopParse = _loopParser.Parse(recipe);

		var timing = _timingCalculator.Calculate(recipe, loopParse.Loops);
		var enrichedLoops = _timingCalculator.EnrichLoopsWithDuration(loopParse.Loops, timing.StepStartTimes);
		var loopStructure = LoopStructure.Create(enrichedLoops);

		var errors = new List<AnalysisError>();
		var warnings = loopParse.Warnings.ToList();

		if (loopParse.HasIntegrityIssues)
		{
			errors.Add(new AnalysisError(null, "Loop structure has integrity issues (unmatched For/EndFor)"));
		}

		var maxDepth = enrichedLoops
			.Where(l => l.Status == LoopStatus.Valid)
			.Select(l => l.NestingDepth)
			.DefaultIfEmpty(0)
			.Max();

		if (maxDepth > MaxLoopDepth)
		{
			errors.Add(new AnalysisError(null, $"Maximum loop nesting depth ({MaxLoopDepth}) exceeded"));
		}

		bool isValid = errors.Count == 0;

		return new AnalysisResult(isValid, loopStructure, timing, errors, warnings);
	}
}
