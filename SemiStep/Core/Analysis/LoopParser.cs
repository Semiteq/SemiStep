using Core.Entities;
using Core.Exceptions;

using Shared.Reasons;

namespace Core.Analysis;

public sealed class LoopParser(CoreConfig config)
{
	private readonly ColumnId _iterationColumnName = config.IterationColumnId;
	public LoopParseResult Parse(Recipe recipe)
	{
		var validLoops = new List<LoopInfo>();
		var reasons = new List<AbstractReason>();
		var stack = new Stack<ForFrame>();

		for (var i = 0; i < recipe.Steps.Count; i++)
		{
			var step = recipe.Steps[i];
			var actionId = step.ActionKey;

			switch (actionId)
			{
				case (int)ServiceActionId.ForLoop:
				{
					var iterations = ExtractIterationCountOrThrow(step);
					var depth = stack.Count + 1;

					stack.Push(new ForFrame(i, iterations, depth));

					break;
				}
				case (int)ServiceActionId.EndForLoop when stack.Count == 0:
				{
					reasons.Add(new LoopIntegrityError($"Unmatched EndFor at step {i}"));

					break;
				}
				case (int)ServiceActionId.EndForLoop:
				{
					var frame = stack.Pop();
					var validLoop = new LoopInfo(
						StartIndex: frame.StartIndex,
						EndIndex: i,
						Depth: frame.Depth,
						Iterations: frame.Iterations);
					validLoops.Add(validLoop);

					break;
				}
			}
		}

		while (stack.Count > 0)
		{
			var frame = stack.Pop();
			reasons.Add(new LoopIntegrityError($"Unclosed For loop starting at step {frame.StartIndex}"));
		}

		return new LoopParseResult(validLoops, reasons);
	}

	private int ExtractIterationCountOrThrow(Step step)
	{
		if (!step.Properties.TryGetValue(_iterationColumnName, out var iterationProperty))
		{
			return 1;
		}

		return iterationProperty.Type switch
		{
			PropertyType.Int => iterationProperty.AsInt(),
			PropertyType.Float => (int)iterationProperty.AsFloat(),
			_ => throw new TypeMismatchException(
				$"Iteration count property has unsupported type '{iterationProperty.Type}' in step {step.ActionKey}.")
		};
	}

	private sealed record ForFrame(int StartIndex, int Iterations, int Depth);
}
