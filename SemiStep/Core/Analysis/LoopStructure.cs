using System.Collections.Immutable;

namespace Core.Analysis;

public sealed record LoopStructure
{
	public IReadOnlyList<LoopInfo> Loops { get; }
	public IReadOnlyDictionary<int, LoopInfo> ByStartIndex { get; }
	public IReadOnlyDictionary<int, IReadOnlyList<LoopInfo>> EnclosingLoopsForStep { get; }

	private LoopStructure(
		IReadOnlyList<LoopInfo> loops,
		IReadOnlyDictionary<int, LoopInfo> byStartIndex,
		IReadOnlyDictionary<int, IReadOnlyList<LoopInfo>> enclosingLoopsForStep)
	{
		Loops = loops;
		ByStartIndex = byStartIndex;
		EnclosingLoopsForStep = enclosingLoopsForStep;
	}

	public static LoopStructure Empty => new(
		ImmutableArray<LoopInfo>.Empty,
		ImmutableDictionary<int, LoopInfo>.Empty,
		ImmutableDictionary<int, IReadOnlyList<LoopInfo>>.Empty);

	public static LoopStructure Create(IReadOnlyList<LoopInfo> loops)
	{
		var byStart = loops
			.Where(l => l.Status != LoopStatus.OrphanEnd)
			.ToDictionary(l => l.StartIndex, l => l);

		var enclosing = BuildEnclosingMap(loops);
		return new LoopStructure(loops, byStart, enclosing);
	}

	private static IReadOnlyDictionary<int, IReadOnlyList<LoopInfo>> BuildEnclosingMap(IReadOnlyList<LoopInfo> loops)
	{
		var builder = new Dictionary<int, List<LoopInfo>>();

		foreach (var loop in loops.Where(l => l.Status != LoopStatus.OrphanEnd && l.EndIndex.HasValue))
		{
			var start = loop.StartIndex;
			var end = loop.EndIndex!.Value;

			for (int i = start + 1; i < end; i++)
			{
				if (!builder.TryGetValue(i, out var list))
				{
					list = [];
					builder[i] = list;
				}

				list.Add(loop);
			}
		}

		return builder.ToDictionary(
			kvp => kvp.Key,
			kvp => (IReadOnlyList<LoopInfo>)kvp.Value
				.OrderBy(l => l.NestingDepth)
				.ToList()
				.AsReadOnly());
	}
}
