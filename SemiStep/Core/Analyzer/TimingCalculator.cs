using Core.Entities;
using Core.Loops;

namespace Core.Analyzer;

/// <summary>
/// Computes linear start times and expands closed loops by iteration count.
/// </summary>
public sealed class TimingCalculator : ITimingCalculator
{
	public TimingResult Calculate(Recipe recipe, LoopSemanticsResult loopSemantics)
	{
		var startTimes = new Dictionary<int, TimeSpan>(recipe.Steps.Count);
		var accumulated = TimeSpan.Zero;

		// Map loop start index to node for iteration expansion
		var loopByEnd = new Dictionary<int, LoopNode>();
		foreach (var node in loopSemantics.Nodes)
		{
			if (node.EndIndex.HasValue && node.Status == LoopStatus.Valid)
				loopByEnd[node.EndIndex.Value] = node;
		}

		for (int i = 0; i < recipe.Steps.Count; i++)
		{
			startTimes[i] = accumulated;

			var step = recipe.Steps[i];
			if (step.DeployDuration == DeployDuration.LongLasting)
			{
				if (step.Properties.TryGetValue(MandatoryColumns.StepDuration, out var durationProperty) &&
					durationProperty != null)
				{
					var valueResult = durationProperty.GetValue<float>();
					if (valueResult.IsSuccess && valueResult.Value > 0f)
					{
						accumulated += TimeSpan.FromSeconds(valueResult.Value);
					}
				}
			}

			if (loopByEnd.TryGetValue(i, out var loopNode))
			{
				// Single iteration duration = accumulated - loop body start time
				var bodyStart = startTimes[loopNode.BodyStartIndex];
				var singleDuration = accumulated - bodyStart;
				if (singleDuration.Ticks < 0)
					singleDuration = TimeSpan.Zero;

				var extraIterations = loopNode.EffectiveIterationCount - 1;
				if (extraIterations > 0)
				{
					accumulated += TimeSpan.FromTicks(singleDuration.Ticks * extraIterations);
				}
			}
		}

		// Update nodes with calculated single iteration duration where possible
		var updatedNodes = new List<LoopNode>(loopSemantics.Nodes.Count);
		foreach (var node in loopSemantics.Nodes)
		{
			if (node.EndIndex.HasValue && node.Status == LoopStatus.Valid)
			{
				var bodyStart = startTimes[node.BodyStartIndex];
				var bodyEndStartTime = startTimes[node.EndIndex.Value];
				var singleDuration = bodyEndStartTime - bodyStart;
				if (singleDuration.Ticks < 0)
					singleDuration = TimeSpan.Zero;

				updatedNodes.Add(node with { SingleIterationDuration = singleDuration });
			}
			else
			{
				updatedNodes.Add(node);
			}
		}

		return new TimingResult(startTimes, accumulated, updatedNodes);
	}
}
