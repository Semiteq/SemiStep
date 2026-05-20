using SemiStep.Core.Plc.State;

namespace SemiStep.Core.Recipes.Analysis;

public static class ExecutionTimeEstimator
{
	public static TimeSpan TimeLeftInStep(
		RecipeSnapshot snapshot,
		PlcExecutionInfo info,
		RecipeMetadataRegistry registry)
	{
		if (info.ActualLine < 0 || info.ActualLine >= snapshot.Recipe.Steps.Count)
		{
			return TimeSpan.Zero;
		}

		var step = snapshot.Recipe.Steps[info.ActualLine];
		var duration = TimingCalculator.ExtractStepDuration(step, registry);
		var elapsed = TimeSpan.FromSeconds(info.StepCurrentTime);

		var remaining = duration - elapsed;
		return remaining.Ticks < 0
			? TimeSpan.Zero
			: remaining;
	}

	public static TimeSpan TimeLeftInRecipe(
		RecipeSnapshot snapshot,
		PlcExecutionInfo info,
		RecipeMetadataRegistry registry)
	{
		_ = registry;

		if (info.ActualLine < 0 || info.ActualLine >= snapshot.Recipe.Steps.Count)
		{
			return TimeSpan.Zero;
		}

		if (!snapshot.StepStartTimes.TryGetValue(info.ActualLine, out var stepStart))
		{
			return TimeSpan.Zero;
		}

		var loopOffset = ComputeLoopOffset(snapshot, info);
		var elapsed = TimeSpan.FromSeconds(info.StepCurrentTime);
		var consumed = stepStart + loopOffset + elapsed;

		if (consumed.Ticks < 0)
		{
			consumed = TimeSpan.Zero;
		}
		if (consumed > snapshot.TotalDuration)
		{
			consumed = snapshot.TotalDuration;
		}

		return snapshot.TotalDuration - consumed;
	}

	private static TimeSpan ComputeLoopOffset(RecipeSnapshot snapshot, PlcExecutionInfo info)
	{
		if (!snapshot.EnclosingLoops.TryGetValue(info.ActualLine, out var enclosing))
		{
			return TimeSpan.Zero;
		}

		var counts = new[] { info.ForLoopCount1, info.ForLoopCount2, info.ForLoopCount3 };

		var offset = TimeSpan.Zero;
		foreach (var loop in enclosing)
		{
			var depthIndex = loop.Depth - 1;
			if (depthIndex < 0 || depthIndex >= counts.Length)
			{
				continue;
			}

			if (!snapshot.SingleIterationDurations.TryGetValue(loop.StartIndex, out var singleIteration))
			{
				continue;
			}

			var completed = counts[depthIndex];
			if (completed <= 0)
			{
				continue;
			}

			offset += TimeSpan.FromTicks(singleIteration.Ticks * completed);
		}

		return offset;
	}
}
