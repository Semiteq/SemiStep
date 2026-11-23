using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>The decoded recipe a run executes, with its loops unrolled into frames.</summary>
internal sealed class RunPlan(RecipeSnapshot snapshot, RecipeMetadataRegistry registry)
{
	private const int MaxLoopDepth = 3;

	public int LineCount => snapshot.Recipe.StepCount;

	public IEnumerable<ExecutionFrame> Frames()
	{
		return UnrollRange(0, LineCount, new int[MaxLoopDepth]);
	}

	private IEnumerable<ExecutionFrame> UnrollRange(int start, int end, int[] loopCounts)
	{
		var line = start;
		while (line < end)
		{
			if (!snapshot.LoopByStart.TryGetValue(line, out var loop))
			{
				yield return StepFrame(line, loopCounts);
				line++;
				continue;
			}

			var depthIndex = loop.Depth - 1;
			loopCounts[depthIndex] = 1;
			yield return ControlFrame(line, loopCounts);

			for (var iteration = 1; iteration <= loop.Iterations; iteration++)
			{
				loopCounts[depthIndex] = iteration;
				foreach (var frame in UnrollRange(loop.StartIndex + 1, loop.EndIndex, loopCounts))
				{
					yield return frame;
				}

				yield return ControlFrame(loop.EndIndex, loopCounts);
			}

			loopCounts[depthIndex] = 0;
			line = loop.EndIndex + 1;
		}
	}

	private ExecutionFrame StepFrame(int line, int[] loopCounts)
	{
		var step = snapshot.Recipe.Steps[line];
		if (step.ActionKey == (int)ServiceActionId.Pause)
		{
			return new ExecutionFrame(line, 0, true, loopCounts[0], loopCounts[1], loopCounts[2]);
		}

		var duration = TimingCalculator.ExtractStepDuration(step, registry);
		var ticks = (int)Math.Ceiling(duration / RunEngine.TickInterval);
		return new ExecutionFrame(line, Math.Max(ticks, 1), false, loopCounts[0], loopCounts[1], loopCounts[2]);
	}

	private static ExecutionFrame ControlFrame(int line, int[] loopCounts)
	{
		return new ExecutionFrame(line, 1, false, loopCounts[0], loopCounts[1], loopCounts[2]);
	}
}
