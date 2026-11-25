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
		PlcExecutionInfo info)
	{
		if (info.ActualLine < 0 || info.ActualLine >= snapshot.Recipe.Steps.Count)
		{
			return TimeSpan.Zero;
		}

		if (info.ActualLine >= snapshot.StepStartTimes.Count)
		{
			return TimeSpan.Zero;
		}

		var stepStart = snapshot.StepStartTimes[info.ActualLine];

		var loopOffset = ComputeLoopOffset(snapshot, info);
		var elapsed = TimeSpan.FromSeconds(info.StepCurrentTime);
		var consumed = stepStart + loopOffset + elapsed;

		if (consumed > snapshot.TotalDuration)
		{
			consumed = snapshot.TotalDuration;
		}

		return snapshot.TotalDuration - consumed;
	}

	private static TimeSpan ComputeLoopOffset(RecipeSnapshot snapshot, PlcExecutionInfo info)
	{
		var counts = new[] { info.ForLoopCount1, info.ForLoopCount2, info.ForLoopCount3 };

		var offset = TimeSpan.Zero;
		if (snapshot.EnclosingLoops.TryGetValue(info.ActualLine, out var enclosing))
		{
			foreach (var loop in enclosing)
			{
				offset += PassesBeforeCurrent(snapshot, loop, counts);
			}
		}

		// The start time of a loop end line holds only the first pass of the loop it closes.
		if (snapshot.LoopByEnd.TryGetValue(info.ActualLine, out var closedLoop))
		{
			offset += PassesBeforeCurrent(snapshot, closedLoop, counts);
		}

		return offset;
	}

	private static TimeSpan PassesBeforeCurrent(RecipeSnapshot snapshot, LoopInfo loop, int[] counts)
	{
		var depthIndex = loop.Depth - 1;
		if (depthIndex < 0 || depthIndex >= counts.Length)
		{
			return TimeSpan.Zero;
		}

		if (!snapshot.SingleIterationDurations.TryGetValue(loop.StartIndex, out var singlePass))
		{
			return TimeSpan.Zero;
		}

		var currentPass = counts[depthIndex];
		if (currentPass <= 0)
		{
			return TimeSpan.Zero;
		}

		var passesBefore = Math.Min(currentPass, loop.Iterations) - 1;
		return singlePass * passesBefore;
	}
}
