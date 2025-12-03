using Core.Entities;
using Core.Loops;
using Core.Reasons.Errors;
using Core.Reasons.Warnings;

using FluentResults;

namespace Core.Analyzer;

/// <summary>
/// Parses For / EndFor constructs producing provisional LoopNode list (status may be incomplete).
/// Extracts iteration count from the 'task' property on For steps.
/// </summary>
public sealed class LoopParser : ILoopParser
{
	private const int ForLoopActionId = (int)ServiceActions.ForLoop;
	private const int EndForLoopActionId = (int)ServiceActions.EndForLoop;

	private sealed record ForFrame(int StartIndex, int IterationCountRaw, int NestingDepth);

	public LoopParseResult Parse(Recipe recipe)
	{
		var nodes = new List<LoopNode>();
		var reasons = new List<IReason>();
		var stack = new Stack<ForFrame>();

		for (int i = 0; i < recipe.Steps.Count; i++)
		{
			var step = recipe.Steps[i];
			var actionIdResult = ExtractActionId(step);
			if (actionIdResult.IsFailed)
			{
				reasons.AddRange(actionIdResult.Errors);
				continue;
			}

			var actionId = actionIdResult.Value;

			if (actionId == ForLoopActionId)
			{
				// Extract iteration count from task property (if present). Default to 1.
				int iterations = ExtractIterationCount(step);

				// Nesting depth = current stack count + 1 (1-based)
				var depth = stack.Count + 1;

				stack.Push(new ForFrame(i, iterations, depth));
			}
			else if (actionId == EndForLoopActionId)
			{
				if (stack.Count == 0)
				{
					// Orphan end
					var orphanNode = new LoopNode(
						StartIndex: i,
						EndIndex: i,
						NestingDepth: 0,
						IterationCountRaw: 1,
						EffectiveIterationCount: 1,
						BodyStartIndex: i,
						BodyEndIndex: i,
						SingleIterationDuration: null,
						Status: LoopStatus.OrphanEnd);
					nodes.Add(orphanNode);
					reasons.Add(new CoreForLoopUnmatchedWarning(i, startIndex: null, endIndex: i,
						details: "Unmatched EndFor"));
				}
				else
				{
					var frame = stack.Pop();
					var provisional = new LoopNode(
						StartIndex: frame.StartIndex,
						EndIndex: i,
						NestingDepth: frame.NestingDepth,
						IterationCountRaw: frame.IterationCountRaw,
						EffectiveIterationCount: frame.IterationCountRaw,
						BodyStartIndex: frame.StartIndex,
						BodyEndIndex: i,
						SingleIterationDuration: null,
						Status: LoopStatus.Valid);
					nodes.Add(provisional);
				}
			}
		}

		while (stack.Count > 0)
		{
			var frame = stack.Pop();
			var incomplete = new LoopNode(
				StartIndex: frame.StartIndex,
				EndIndex: null,
				NestingDepth: frame.NestingDepth,
				IterationCountRaw: frame.IterationCountRaw,
				EffectiveIterationCount: frame.IterationCountRaw,
				BodyStartIndex: frame.StartIndex,
				BodyEndIndex: null,
				SingleIterationDuration: null,
				Status: LoopStatus.Incomplete);
			nodes.Add(incomplete);
			reasons.Add(new CoreForLoopUnmatchedWarning(frame.StartIndex, startIndex: frame.StartIndex, endIndex: null,
				details: "Unclosed ForLoop"));
		}

		return new LoopParseResult(nodes, reasons);
	}

	private static int ExtractIterationCount(Step step)
	{
		if (!step.Properties.TryGetValue(MandatoryColumns.Task, out var taskProperty) || taskProperty == null)
			return 1;

		var getValueResult = taskProperty.GetValue<float>();
		if (getValueResult.IsFailed)
			return 1;

		// Convert float to int similarly to previous implementation
		var raw = (int)getValueResult.Value;
		return raw;
	}

	private static Result<int> ExtractActionId(Step step)
	{
		if (!step.Properties.TryGetValue(MandatoryColumns.Action, out var actionProperty) || actionProperty == null)
			return Result.Fail(new CoreStepNoActionPropertyError());

		var g = actionProperty.GetValue<short>();
		if (g.IsFailed)
			return g.ToResult<int>();
		return g.Value;
	}
}
