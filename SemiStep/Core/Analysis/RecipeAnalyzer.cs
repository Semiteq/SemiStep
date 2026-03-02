using Core.Entities;

using Shared.Reasons;

namespace Core.Analysis;

public sealed class RecipeAnalyzer(TimingCalculator timingCalculator, LoopParser loopParser)
{
	private const int MaxLoopDepth = 3;

	public RecipeSnapshot Analyze(Recipe recipe)
	{
		if (recipe.Steps.Count == 0)
		{
			return RecipeSnapshot.Create(
				recipe,
				TimeSpan.Zero,
				new Dictionary<int, TimeSpan>(),
				[],
				[new EmptyRecipeWarning("Recipe is empty")]);
		}

		var loopParse = loopParser.Parse(recipe);
		var reasons = new List<AbstractReason>(loopParse.Reasons);

		var (stepStartTimes, totalDuration) = timingCalculator.Calculate(recipe, loopParse.Loops);

		var maxDepth = loopParse.Loops.Count > 0
			? loopParse.Loops.Max(l => l.Depth)
			: 0;

		if (maxDepth > MaxLoopDepth)
		{
			reasons.Add(new LoopNestingDepthError($"Maximum loop nesting depth ({MaxLoopDepth}) exceeded: {maxDepth}"));
		}

		return RecipeSnapshot.Create(
			recipe,
			totalDuration,
			stepStartTimes,
			loopParse.Loops,
			reasons);
	}
}
