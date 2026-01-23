using Recipe.Entities;

using Shared.Registries;

namespace Recipe.Analysis;

public sealed class LoopParser(IActionRegistry actionRegistry)
{
	public LoopParseResult Parse(Entities.Recipe recipe)
	{
		var loops = new List<LoopInfo>();
		var warnings = new List<AnalysisWarning>();
		var stack = new Stack<ForFrame>();

		for (int i = 0; i < recipe.Steps.Count; i++)
		{
			var step = recipe.Steps[i];
			var actionId = GetActionId(step.ActionKey);

			if (actionId == null)
			{
				continue;
			}

			if (actionId == (short)ServiceActionId.ForLoop)
			{
				int iterations = ExtractIterationCount(step);
				var depth = stack.Count + 1;
				stack.Push(new ForFrame(i, iterations, depth));
			}
			else if (actionId == (short)ServiceActionId.EndForLoop)
			{
				if (stack.Count == 0)
				{
					var orphanLoop = new LoopInfo(
						StartIndex: i,
						EndIndex: i,
						NestingDepth: 0,
						IterationCount: 1,
						SingleIterationDuration: null,
						Status: LoopStatus.OrphanEnd);
					loops.Add(orphanLoop);
					warnings.Add(new AnalysisWarning(i, "Unmatched EndFor"));
				}
				else
				{
					var frame = stack.Pop();
					var validLoop = new LoopInfo(
						StartIndex: frame.StartIndex,
						EndIndex: i,
						NestingDepth: frame.NestingDepth,
						IterationCount: frame.IterationCount,
						SingleIterationDuration: null,
						Status: LoopStatus.Valid);
					loops.Add(validLoop);
				}
			}
		}

		while (stack.Count > 0)
		{
			var frame = stack.Pop();
			var incompleteLoop = new LoopInfo(
				StartIndex: frame.StartIndex,
				EndIndex: null,
				NestingDepth: frame.NestingDepth,
				IterationCount: frame.IterationCount,
				SingleIterationDuration: null,
				Status: LoopStatus.Incomplete);
			loops.Add(incompleteLoop);
			warnings.Add(new AnalysisWarning(frame.StartIndex, "Unclosed ForLoop"));
		}

		return new LoopParseResult(loops, warnings);
	}

	private short? GetActionId(string actionKey)
	{
		if (!short.TryParse(actionKey, out var actionId))
		{
			return null;
		}

		if (!actionRegistry.ActionExists(actionId))
		{
			return null;
		}

		return actionId;
	}

	private static int ExtractIterationCount(Step step)
	{
		var iterationColumn = new ColumnId("iterations");
		if (!step.Properties.TryGetValue(iterationColumn, out var iterationProperty))
		{
			return 1;
		}

		return iterationProperty.Type switch
		{
			PropertyType.Int => iterationProperty.AsInt(),
			PropertyType.Float => (int)iterationProperty.AsFloat(),
			_ => 1
		};
	}

	private sealed record ForFrame(int StartIndex, int IterationCount, int NestingDepth);
}

public sealed record LoopParseResult(
	IReadOnlyList<LoopInfo> Loops,
	IReadOnlyList<AnalysisWarning> Warnings)
{
	public bool HasIntegrityIssues => Loops.Any(l => l.Status != LoopStatus.Valid);
}
