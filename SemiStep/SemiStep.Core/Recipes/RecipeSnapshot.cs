namespace SemiStep.Core.Recipes;

public record struct RecipeSnapshot(
	Recipe Recipe,
	TimeSpan TotalDuration,
	IReadOnlyList<TimeSpan> StepStartTimes,
	IReadOnlyList<LoopInfo> Loops,
	IReadOnlyDictionary<int, LoopInfo> LoopByStart,
	IReadOnlyDictionary<int, LoopInfo> LoopByEnd,
	IReadOnlyDictionary<int, IReadOnlyList<LoopInfo>> EnclosingLoops,
	IReadOnlyDictionary<int, TimeSpan> SingleIterationDurations,
	IReadOnlyList<int> RowLoopDepths)
{
	public static readonly RecipeSnapshot Empty = new(
		Recipe.Empty,
		TimeSpan.Zero,
		[],
		[],
		new Dictionary<int, LoopInfo>(),
		new Dictionary<int, LoopInfo>(),
		new Dictionary<int, IReadOnlyList<LoopInfo>>(),
		new Dictionary<int, TimeSpan>(),
		[]);

	public static RecipeSnapshot Create(
		Recipe recipe,
		TimeSpan totalDuration,
		IReadOnlyList<TimeSpan> stepStartTimes,
		IReadOnlyList<LoopInfo> loops,
		IReadOnlyDictionary<int, TimeSpan> singleIterationDurations)
	{
		var byStart = loops.ToDictionary(l => l.StartIndex, l => l);
		var byEnd = loops.ToDictionary(l => l.EndIndex, l => l);
		var enclosing = BuildEnclosingMap(loops);
		var rowLoopDepths = BuildRowLoopDepths(recipe.StepCount, loops);

		return new RecipeSnapshot(
			recipe,
			totalDuration,
			stepStartTimes,
			loops,
			byStart,
			byEnd,
			enclosing,
			singleIterationDurations,
			rowLoopDepths);
	}

	private static IReadOnlyList<int> BuildRowLoopDepths(int stepCount, IReadOnlyList<LoopInfo> loops)
	{
		var depths = new int[stepCount];

		foreach (var loop in loops)
		{
			var depth = loop.Depth;
			for (var i = loop.StartIndex; i <= loop.EndIndex; i++)
			{
				if (depths[i] < depth)
				{
					depths[i] = depth;
				}
			}
		}

		return depths;
	}

	private static Dictionary<int, IReadOnlyList<LoopInfo>> BuildEnclosingMap(IReadOnlyList<LoopInfo> loops)
	{
		var builder = new Dictionary<int, IReadOnlyList<LoopInfo>>();

		foreach (var loop in loops)
		{
			for (var i = loop.StartIndex + 1; i < loop.EndIndex; i++)
			{
				if (builder.TryGetValue(i, out var existing))
				{
					((List<LoopInfo>)existing).Add(loop);
				}
				else
				{
					builder[i] = new List<LoopInfo> { loop };
				}
			}
		}

		// In-place unstable sort is safe: loops enclosing a given row are strictly nested,
		// so their depths are distinct and no tie can reorder equal keys.
		foreach (var list in builder.Values)
		{
			((List<LoopInfo>)list).Sort(static (left, right) => left.Depth.CompareTo(right.Depth));
		}

		return builder;
	}
}
