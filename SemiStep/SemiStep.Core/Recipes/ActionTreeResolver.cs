using FluentResults;

namespace SemiStep.Core.Recipes;

/// <summary>
/// Resolves the reference graph of mapped actions into the storage/serialization view.
/// For every <see cref="ActionRole.Action"/> root it walks each selector column's
/// <c>targets</c> transitively and materializes:
/// <list type="bullet">
/// <item>the column UNION in deterministic order (declaration order, depth-first splice at
/// the selector site: a selector's child columns are inserted right after the selector
/// column), deduped by key; and</item>
/// <item>the per-column activation condition (the chain of selector values up the path that
/// make the column active).</item>
/// </list>
/// The resolved primary <see cref="ActionDefinition"/>s (with the union as their
/// <see cref="ActionDefinition.Properties"/>) are the product. Subactions are consumed only
/// during the walk and are not part of the output.
/// </summary>
public static class ActionTreeResolver
{
	public static Result<IReadOnlyList<ActionDefinition>> Resolve(
		IReadOnlyCollection<ActionDefinition> actions)
	{
		ArgumentNullException.ThrowIfNull(actions);

		// Ids are unique upstream (the registry passes a dictionary's values; the loader rejects
		// cross-file duplicates), so last-write-wins is safe and needs no dedicated duplicate guard.
		var actionsById = new Dictionary<int, ActionDefinition>();
		foreach (var action in actions)
		{
			actionsById[action.Id] = action;
		}

		var roots = actions
			.Where(action => action.Role == ActionRole.Action)
			.ToList();

		var resolved = new List<ActionDefinition>(roots.Count);

		foreach (var root in roots)
		{
			var rootResult = ResolveRoot(root, actionsById);
			if (rootResult.IsFailed)
			{
				return rootResult.ToResult<IReadOnlyList<ActionDefinition>>();
			}

			resolved.Add(rootResult.Value);
		}

		return Result.Ok<IReadOnlyList<ActionDefinition>>(resolved);
	}

	private static Result<ActionDefinition> ResolveRoot(
		ActionDefinition root,
		IReadOnlyDictionary<int, ActionDefinition> actionsById)
	{
		var union = new List<ActionPropertyDefinition>();
		var byKey = new Dictionary<string, ActionPropertyDefinition>(StringComparer.OrdinalIgnoreCase);
		var onPath = new HashSet<int>();

		var walkResult = Walk(
			root,
			root.Id,
			Array.Empty<ActivationCondition>(),
			actionsById,
			union,
			byKey,
			onPath);

		if (walkResult.IsFailed)
		{
			return walkResult.ToResult<ActionDefinition>();
		}

		return Result.Ok(new ActionDefinition(
			id: root.Id,
			uiName: root.UiName,
			deployDuration: root.DeployDuration,
			properties: union,
			formula: root.Formula,
			role: root.Role));
	}

	private static Result Walk(
		ActionDefinition action,
		int rootId,
		IReadOnlyList<ActivationCondition> pathConditions,
		IReadOnlyDictionary<int, ActionDefinition> actionsById,
		List<ActionPropertyDefinition> union,
		Dictionary<string, ActionPropertyDefinition> byKey,
		HashSet<int> onPath)
	{
		if (!onPath.Add(action.Id))
		{
			return Result.Fail(
				$"Cycle detected in action reference graph at action id {action.Id}");
		}

		try
		{
			foreach (var column in action.Properties)
			{
				var appendResult = AppendColumn(column, rootId, pathConditions, union, byKey);
				if (appendResult.IsFailed)
				{
					return appendResult;
				}

				if (column.Targets is null || column.Targets.Count == 0)
				{
					continue;
				}

				foreach (var (selectorValue, targetId) in column.Targets.OrderBy(entry => entry.Key))
				{
					if (!actionsById.TryGetValue(targetId, out var subaction))
					{
						return Result.Fail(
							$"Column '{column.Key}' targets undefined action id {targetId}");
					}

					var childConditions = new List<ActivationCondition>(pathConditions.Count + 1);
					childConditions.AddRange(pathConditions);
					childConditions.Add(new ActivationCondition(column.Key, selectorValue));

					var childResult = Walk(
						subaction,
						rootId,
						childConditions,
						actionsById,
						union,
						byKey,
						onPath);

					if (childResult.IsFailed)
					{
						return childResult;
					}
				}
			}

			return Result.Ok();
		}
		finally
		{
			onPath.Remove(action.Id);
		}
	}

	private static Result AppendColumn(
		ActionPropertyDefinition column,
		int rootId,
		IReadOnlyList<ActivationCondition> pathConditions,
		List<ActionPropertyDefinition> union,
		Dictionary<string, ActionPropertyDefinition> byKey)
	{
		var activation = pathConditions.Count == 0
			? null
			: pathConditions.ToList();

		if (byKey.TryGetValue(column.Key, out var existing))
		{
			if (!string.Equals(existing.PropertyTypeId, column.PropertyTypeId, StringComparison.Ordinal))
			{
				return Result.Fail(
					$"Column key '{column.Key}' is reachable with conflicting property types "
					+ $"'{existing.PropertyTypeId}' and '{column.PropertyTypeId}'");
			}

			// Reached a second time within the SAME root. If the two paths carry different
			// activation conditions, only the first path's conditions would survive on the
			// resolved column, silently greying the column wrongly under the second branch.
			// Representing OR-of-paths is out of scope; reject the ambiguous authoring instead.
			var existingActivation = existing.Activation ?? Enumerable.Empty<ActivationCondition>();
			var candidateActivation = activation ?? Enumerable.Empty<ActivationCondition>();
			if (!existingActivation.SequenceEqual(candidateActivation))
			{
				return Result.Fail(
					$"Column key '{column.Key}' is reachable from two distinct branches of action "
					+ $"id {rootId} with different activation conditions; a subaction may not be "
					+ "shared across branches of the same root");
			}

			return Result.Ok();
		}

		var resolvedColumn = column with
		{
			Targets = null,
			Activation = activation
		};

		union.Add(resolvedColumn);
		byKey[column.Key] = resolvedColumn;

		return Result.Ok();
	}
}
