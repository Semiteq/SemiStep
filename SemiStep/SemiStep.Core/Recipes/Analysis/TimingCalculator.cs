namespace SemiStep.Core.Recipes.Analysis;

internal static class TimingCalculator
{
	private static readonly PropertyId _durationProperty = new("step_duration");

	public static (
		IReadOnlyDictionary<int, TimeSpan> StepStartTimes,
		TimeSpan TotalDuration,
		IReadOnlyDictionary<int, TimeSpan> SingleIterationDurations) Calculate(
		Recipe recipe,
		IReadOnlyList<LoopInfo> loops,
		RecipeMetadataRegistry registry)
	{
		var startTimes = new Dictionary<int, TimeSpan>(recipe.Steps.Count);
		var singleIterations = new Dictionary<int, TimeSpan>();
		var accumulated = TimeSpan.Zero;

		var loopByEnd = loops.ToDictionary(l => l.EndIndex, l => l);

		for (var i = 0; i < recipe.Steps.Count; i++)
		{
			startTimes[i] = accumulated;

			var step = recipe.Steps[i];
			var duration = ExtractStepDuration(step, registry);
			if (duration > TimeSpan.Zero)
			{
				accumulated += duration;
			}

			if (loopByEnd.TryGetValue(i, out var loopInfo))
			{
				var bodyStartTime = startTimes[loopInfo.StartIndex];
				var singleDuration = accumulated - bodyStartTime;
				if (singleDuration.Ticks < 0)
				{
					singleDuration = TimeSpan.Zero;
				}

				singleIterations[loopInfo.StartIndex] = singleDuration;

				var extraIterations = loopInfo.Iterations - 1;
				if (extraIterations > 0)
				{
					accumulated += TimeSpan.FromTicks(singleDuration.Ticks * extraIterations);
				}
			}
		}

		return (startTimes, accumulated, singleIterations);
	}

	internal static TimeSpan ExtractStepDuration(Step step, RecipeMetadataRegistry registry)
	{
		var actionResult = registry.GetAction(step.ActionKey);
		if (actionResult.IsFailed)
		{
			throw new InvalidOperationException(
				$"Step references unknown action id {step.ActionKey}");
		}

		if (actionResult.Value.DeployDuration == DeployDuration.Immediate)
		{
			return TimeSpan.Zero;
		}

		if (!step.Properties.TryGetValue(_durationProperty, out var durationProperty))
		{
			return TimeSpan.Zero;
		}

		return durationProperty.Type switch
		{
			PropertyType.Float => TimeSpan.FromSeconds(durationProperty.AsFloat()),
			PropertyType.Int => TimeSpan.FromSeconds(durationProperty.AsInt()),
			_ => TimeSpan.Zero
		};
	}
}
