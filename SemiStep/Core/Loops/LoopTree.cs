using System.Collections.Immutable;

namespace Core.Loops;

/// <summary>
/// Immutable representation of all loops in a recipe with fast lookup maps.
/// EnclosingLoopsForStep lists loops that contain the step, ordered outer → inner.
/// </summary>
public sealed record LoopTree
{
	public IReadOnlyList<LoopNode> Nodes { get; }
	public IReadOnlyDictionary<int, LoopNode> ByStartIndex { get; }
	public IReadOnlyDictionary<int, IReadOnlyList<LoopNode>> EnclosingLoopsForStep { get; }

	public LoopTree(
		IReadOnlyList<LoopNode> nodes,
		IReadOnlyDictionary<int, LoopNode> byStartIndex,
		IReadOnlyDictionary<int, IReadOnlyList<LoopNode>> enclosingLoopsForStep)
	{
		Nodes = nodes;
		ByStartIndex = byStartIndex;
		EnclosingLoopsForStep = enclosingLoopsForStep;
	}

	public static LoopTree Empty =>
		new(
			ImmutableArray<LoopNode>.Empty,
			ImmutableDictionary<int, LoopNode>.Empty,
			ImmutableDictionary<int, IReadOnlyList<LoopNode>>.Empty);

	public static LoopTree Create(IReadOnlyList<LoopNode> nodes)
	{
		var byStart = nodes
			.Where(n => n.Status != LoopStatus.OrphanEnd)
			.ToDictionary(n => n.StartIndex, n => n);

		var enclosing = BuildEnclosingMap(nodes);
		return new LoopTree(nodes, byStart, enclosing);
	}

	private static IReadOnlyDictionary<int, IReadOnlyList<LoopNode>> BuildEnclosingMap(IReadOnlyList<LoopNode> nodes)
	{
		var builder = new Dictionary<int, List<LoopNode>>();

		foreach (var loop in nodes.Where(l => l.Status != LoopStatus.OrphanEnd && l.EndIndex.HasValue))
		{
			var start = loop.StartIndex;
			var end = loop.EndIndex!.Value;

			for (int i = start + 1; i < end; i++)
			{
				if (!builder.TryGetValue(i, out var list))
				{
					list = new List<LoopNode>();
					builder[i] = list;
				}

				list.Add(loop);
			}
		}

		return builder.ToDictionary(
			kvp => kvp.Key,
			kvp => (IReadOnlyList<LoopNode>)kvp.Value
				.OrderBy(l => l.NestingDepth) // outer → inner
				.ToList()
				.AsReadOnly());
	}
}
