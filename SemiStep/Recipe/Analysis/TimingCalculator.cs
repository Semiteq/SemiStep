using Recipe.Entities;

namespace Recipe.Analysis;

public sealed class TimingCalculator
{
	private static readonly ColumnId _durationColumn = new("duration");

	public TimingResult Calculate(Entities.Recipe recipe, IReadOnlyList<LoopInfo> loops)
	{
		var startTimes = new Dictionary<int, TimeSpan>(recipe.Steps.Count);
		var accumulated = TimeSpan.Zero;

		var loopByEnd = loops
			.Where(l => l.EndIndex.HasValue && l.Status == LoopStatus.Valid)
			.ToDictionary(l => l.EndIndex!.Value, l => l);

		for (int i = 0; i < recipe.Steps.Count; i++)
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
					singleDuration = TimeSpan.Zero;

				var extraIterations = loopInfo.IterationCount - 1;
				if (extraIterations > 0)
				{
					accumulated += TimeSpan.FromTicks(singleDuration.Ticks * extraIterations);
				}
			}
		}

		return new TimingResult(startTimes, accumulated);
	}

	public IReadOnlyList<LoopInfo> EnrichLoopsWithDuration(
		IReadOnlyList<LoopInfo> loops,
		IReadOnlyDictionary<int, TimeSpan> startTimes)
	{
		var result = new List<LoopInfo>(loops.Count);

		foreach (var loop in loops)
		{
			if (loop.EndIndex.HasValue && loop.Status == LoopStatus.Valid)
			{
				var bodyStartTime = startTimes[loop.StartIndex];
				var endStartTime = startTimes[loop.EndIndex.Value];
				var singleDuration = endStartTime - bodyStartTime;
				if (singleDuration.Ticks < 0)
					singleDuration = TimeSpan.Zero;

				result.Add(loop with { SingleIterationDuration = singleDuration });
			}
			else
			{
				result.Add(loop);
			}
		}

		return result;
	}

	private static TimeSpan ExtractStepDuration(Step step)
	{
		if (!step.Properties.TryGetValue(_durationColumn, out var durationProperty))
			return TimeSpan.Zero;

		return durationProperty.Type switch
		{
			PropertyType.Float => TimeSpan.FromSeconds(durationProperty.AsFloat()),
			PropertyType.Int => TimeSpan.FromSeconds(durationProperty.AsInt()),
			_ => TimeSpan.Zero
		};
	}
}
