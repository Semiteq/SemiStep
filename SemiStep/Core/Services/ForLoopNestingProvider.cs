using Core.Facade;
using Core.Loops;

namespace Core.Services;

public sealed class ForLoopNestingProvider : IForLoopNestingProvider
{
	private readonly IRecipeFacade _facade;
	private readonly ILogger<ForLoopNestingProvider> _logger;

	public ForLoopNestingProvider(IRecipeFacade facade, ILogger<ForLoopNestingProvider> logger)
	{
		_facade = facade ?? throw new ArgumentNullException(nameof(facade));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public int GetNestingDepth(int stepIndex)
	{
		try
		{
			var snapshot = _facade.CurrentSnapshot;
			var stepCount = snapshot.StepCount;
			if (stepIndex < 0 || stepIndex >= stepCount)
				return 0;

			var loopTree = snapshot.LoopTree;

			// 1) For line itself
			if (loopTree.ByStartIndex.TryGetValue(stepIndex, out var startNode))
			{
				return NormalizeDepth(startNode.NestingDepth);
			}

			// 2) EndFor line
			var endNode = loopTree.Nodes.FirstOrDefault(n =>
				n.EndIndex.HasValue && n.EndIndex.Value == stepIndex && n.Status != LoopStatus.OrphanEnd);
			if (endNode != null)
			{
				return NormalizeDepth(endNode.NestingDepth);
			}

			// 3) Enclosed step within complete loops (outer -> inner order)
			if (loopTree.EnclosingLoopsForStep.TryGetValue(stepIndex, out var containing))
			{
				if (containing.Count > 0)
					return NormalizeDepth(containing[containing.Count - 1].NestingDepth);
			}

			// 4) Inside incomplete loops (unclosed For) -> treat as enclosed
			var incompleteDepth = loopTree.Nodes
				.Where(n => n.Status == LoopStatus.Incomplete && stepIndex > n.StartIndex)
				.Select(n => n.NestingDepth)
				.DefaultIfEmpty(0)
				.Max();
			return NormalizeDepth(incompleteDepth);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to compute loop nesting for step {Index}", stepIndex);
			return 0;
		}
	}

	private static int NormalizeDepth(int depth)
	{
		if (depth < 0)
			return 0;
		if (depth > 3)
			return 3;
		return depth;
	}
}
