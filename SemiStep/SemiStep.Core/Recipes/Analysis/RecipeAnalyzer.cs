using FluentResults;

namespace SemiStep.Core.Recipes.Analysis;

public sealed class RecipeAnalyzer(RecipeMetadataRegistry registry)
{
	private const int MaxLoopDepth = 3;

	public Result<RecipeSnapshot> Analyze(Recipe recipe)
	{
		var loopParseResult = LoopParser.Parse(recipe);
		if (loopParseResult.IsFailed)
		{
			return Result.Fail(loopParseResult.Errors);
		}

		var parsedLoops = loopParseResult.Value;

		var (stepStartTimes, totalDuration, singleIterationDurations) = TimingCalculator.Calculate(recipe, parsedLoops, registry);

		var maxDepth = parsedLoops.Count > 0
			? parsedLoops.Max(l => l.Depth)
			: 0;

		if (maxDepth > MaxLoopDepth)
		{
			return Result.Fail($"Maximum loop nesting depth ({MaxLoopDepth}) exceeded: {maxDepth}");
		}

		var snapshot = RecipeSnapshot.Create(
			recipe,
			totalDuration,
			stepStartTimes,
			parsedLoops,
			singleIterationDurations);

		return Result.Ok(snapshot)
			.WithReasons(loopParseResult.Reasons);
	}
}
