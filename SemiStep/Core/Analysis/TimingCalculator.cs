using Core.Entities;

namespace Core.Analysis;

public sealed class TimingCalculator
{
	private static readonly ColumnId _durationColumn = new("step_duration");

	public (IReadOnlyDictionary<int, TimeSpan> StepStartTimes, TimeSpan TotalDuration) Calculate(
		Recipe recipe,
		IReadOnlyList<LoopInfo> loops)
	{
		var startTimes = new Dictionary<int, TimeSpan>(recipe.Steps.Count);
		var accumulated = TimeSpan.Zero;

		var loopByEnd = loops.ToDictionary(l => l.EndIndex, l => l);

		for (var i = 0; i < recipe.Steps.Count; i++)
		{
			startTimes[i] = accumulated;

			var step = recipe.Steps[i];
			var duration = ExtractStepDuration(step);
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

				var extraIterations = loopInfo.Iterations - 1;
				if (extraIterations > 0)
				{
					accumulated += TimeSpan.FromTicks(singleDuration.Ticks * extraIterations);
				}
			}
		}

		return (startTimes, accumulated);
	}

	private static TimeSpan ExtractStepDuration(Step step)
	{
		if (!step.Properties.TryGetValue(_durationColumn, out var durationProperty))
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
