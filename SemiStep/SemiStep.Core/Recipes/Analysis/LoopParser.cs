using FluentResults;

using SemiStep.Core.Shared;

namespace SemiStep.Core.Recipes.Analysis;

internal sealed class LoopParser
{
	private const string IterationColumnName = "task";
	private static readonly PropertyId _iterationPropertyId = new(IterationColumnName);

	public Result<List<LoopInfo>> Parse(Recipe recipe)
	{
		var validLoops = new List<LoopInfo>();
		var reasons = new List<IReason>();
		var stack = new Stack<ForFrame>();

		for (var i = 0; i < recipe.Steps.Count; i++)
		{
			var step = recipe.Steps[i];
			var actionId = step.ActionKey;

			switch (actionId)
			{
				case (int)ServiceActionId.ForLoop:
				{
					var iterationsResult = ExtractIterationCount(step);

					if (iterationsResult.IsFailed)
					{
						return Result
							.Fail(iterationsResult.Errors)
							.WithReasons(reasons);
					}

					var iterations = iterationsResult.Value;
					var depth = stack.Count + 1;

					stack.Push(new ForFrame(i, iterations, depth));

					break;
				}
				case (int)ServiceActionId.EndForLoop when stack.Count == 0:
				{
					reasons.Add(new Warning($"Unmatched EndFor at step {i}"));

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
			reasons.Add(new Warning($"Unclosed For loop starting at step {frame.StartIndex}"));
		}

		return Result
			.Ok(validLoops)
			.WithReasons(reasons);
	}

	private Result<int> ExtractIterationCount(Step step)
	{
		if (!step.Properties.TryGetValue(_iterationPropertyId, out var iterationProperty))
		{
			return 1;
		}

		return iterationProperty.Type switch
		{
			PropertyType.Int => iterationProperty.AsInt(),
			PropertyType.Float => (int)iterationProperty.AsFloat(),
			_ => new Error($"Iteration count property has unsupported type " +
						   $"'{iterationProperty.Type}' in step {step.ActionKey}")
		};
	}

	private sealed record ForFrame(int StartIndex, int Iterations, int Depth);
}
