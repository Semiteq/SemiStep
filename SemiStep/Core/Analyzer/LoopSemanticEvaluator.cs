using Core.Entities;
using Core.Loops;
using Core.Reasons.Warnings;

using FluentResults;

namespace Core.Analyzer;

/// <summary>
/// Applies semantic rules: iteration extraction, max depth detection, integrity flags.
/// </summary>
public sealed class LoopSemanticEvaluator : ILoopSemanticEvaluator
{
	private const int ForLoopActionId = (int)ServiceActions.ForLoop;
	private const int MaxDepth = 3;

	public LoopSemanticsResult Evaluate(LoopParseResult parseResult)
	{
		var reasons = new List<IReason>();
		bool loopIntegrity = false;
		bool maxDepthExceeded = false;
		var enriched = new List<LoopNode>();

		foreach (var node in parseResult.Nodes)
		{
			var status = node.Status;
			if (status == LoopStatus.Incomplete || status == LoopStatus.OrphanEnd)
				loopIntegrity = true;

			int effectiveIterations = node.IterationCountRaw <= 0 ? 1 : node.IterationCountRaw;

			if (node.NestingDepth > MaxDepth)
			{
				maxDepthExceeded = true;
				reasons.Add(new CoreForLoopMaxDepthExceededWarning(node.StartIndex, MaxDepth));
			}

			var enrichedNode = node with
			{
				EffectiveIterationCount = effectiveIterations
			};

			enriched.Add(enrichedNode);
		}

		// Semantic evaluator отвечает только за свои семантические причины.
		// Parse‑причины (parseResult.Reasons) добавляются на уровне RecipeAnalyzer.

		return new LoopSemanticsResult(enriched, reasons, loopIntegrity, maxDepthExceeded);
	}
}
