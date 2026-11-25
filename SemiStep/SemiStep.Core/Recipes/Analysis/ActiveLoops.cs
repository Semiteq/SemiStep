using SemiStep.Core.Plc.State;

namespace SemiStep.Core.Recipes.Analysis;

/// <summary>A loop the PLC is running, with the pass its counter reports (1-based).</summary>
public sealed record ActiveLoop(LoopInfo Loop, int Pass);

public static class ActiveLoops
{
	/// <summary>
	/// The loops around the PLC line, innermost first, start and end rows included; a loop counts only while
	/// the counter of its depth is 1 or more.
	/// </summary>
	public static IReadOnlyList<ActiveLoop> Resolve(RecipeSnapshot snapshot, PlcExecutionInfo info)
	{
		if (!info.RecipeActive)
		{
			return [];
		}

		int[] counters = [info.ForLoopCount1, info.ForLoopCount2, info.ForLoopCount3];
		var active = new List<ActiveLoop>();

		foreach (var loop in snapshot.Loops)
		{
			var isAround = loop.StartIndex <= info.ActualLine && info.ActualLine <= loop.EndIndex;
			if (!isAround || loop.Depth < 1 || loop.Depth > counters.Length)
			{
				continue;
			}

			var pass = counters[loop.Depth - 1];
			if (pass >= 1)
			{
				active.Add(new ActiveLoop(loop, pass));
			}
		}

		active.Sort((left, right) => right.Loop.Depth.CompareTo(left.Loop.Depth));

		return active;
	}
}
