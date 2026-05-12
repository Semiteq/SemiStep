using FluentResults;

using SemiStep.Core.Shared;

namespace SemiStep.Core.Recipes.Analysis;

public sealed class RecipeAnalyzer(RecipeMetadataRegistry registry)
{
	private const int MaxLoopDepth = 3;

	public Result<RecipeSnapshot> Analyze(Recipe recipe)
	{

		if (recipe.Steps.Count == 0)
		{
			return Result.Ok(RecipeSnapshot.Empty).WithWarning("Recipe has no steps");
		}

		var loopParseResult = LoopParser.Parse(recipe);
		if (loopParseResult.IsFailed)
		{
			return Result.Fail(loopParseResult.Errors);
		}

		var parsedLoops = loopParseResult.Value;

		var (stepStartTimes, totalDuration) = TimingCalculator.Calculate(recipe, parsedLoops, registry);

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
			parsedLoops);

		return Result.Ok(snapshot)
			.WithReasons(loopParseResult.Reasons);
	}
}
