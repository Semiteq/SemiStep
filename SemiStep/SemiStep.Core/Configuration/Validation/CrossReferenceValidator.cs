using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Validation;

internal static class CrossReferenceValidator
{
	public static Result Validate(
		List<PropertyDto> properties,
		List<ColumnDto> columns,
		Dictionary<string, Dictionary<int, string>> groups,
		List<ActionDto> actions)
	{
		var propertyIds = properties
			.Where(p => !string.IsNullOrEmpty(p.PropertyTypeId))
			.Select(p => p.PropertyTypeId!)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var columnKeys = columns
			.Where(c => !string.IsNullOrEmpty(c.Key))
			.Select(c => c.Key!)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var groupIds = groups.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var validationResults = new List<Result>();

		ValidateColumnReferences(columns, propertyIds, validationResults);
		ValidateActionReferences(actions, propertyIds, columnKeys, groupIds, validationResults);
		ValidateReferenceGraph(actions, validationResults);

		if (validationResults.Count == 0)
		{
			return Result.Ok();
		}

		return Result.Merge(validationResults.ToArray());
	}

	private static void ValidateReferenceGraph(
		List<ActionDto> actions,
		List<Result> validationResults)
	{
		// Duplicate action ids are rejected earlier by ActionsSectionLoader (across files) and
		// by domain mapping; here the list is already unique. Last-write-wins keeps this robust
		// if that ever changes without masking the dedicated duplicate-id error.
		var actionsById = new Dictionary<int, ActionDto>();
		foreach (var action in actions)
		{
			actionsById[action.Id] = action;
		}

		var subactionIds = actions
			.Where(IsSubaction)
			.Select(action => action.Id)
			.ToHashSet();

		var referencedTargetIds = new HashSet<int>();

		foreach (var action in actions)
		{
			if (action.Columns == null)
			{
				continue;
			}

			var actionLocation = $"actions, Id={action.Id}, UiName='{action.UiName}'";

			foreach (var column in action.Columns)
			{
				if (column.Targets == null || column.Targets.Count == 0)
				{
					continue;
				}

				foreach (var (selectorValue, targetId) in column.Targets)
				{
					referencedTargetIds.Add(targetId);

					if (!actionsById.TryGetValue(targetId, out var target))
					{
						validationResults.Add(Result.Fail(
							$"[{actionLocation}] Column '{column.Key}' targets undefined action id {targetId} "
							+ $"(selector value {selectorValue})"));

						continue;
					}

					if (!IsSubaction(target))
					{
						validationResults.Add(Result.Fail(
							$"[{actionLocation}] Column '{column.Key}' targets action id {targetId} which is "
							+ $"role 'action'; targets must point at a 'subaction'"));
					}
				}
			}
		}

		foreach (var subactionId in subactionIds)
		{
			if (!referencedTargetIds.Contains(subactionId))
			{
				validationResults.Add(Result.Fail(
					$"[actions, Id={subactionId}] Subaction id {subactionId} is not referenced by any "
					+ $"column 'targets' (orphan subaction)"));
			}
		}

		ValidateNoCycles(actions, actionsById, validationResults);
		ValidateNoSharedColumnAcrossBranches(actions, actionsById, validationResults);
	}

	/// <summary>
	/// Mirrors the resolver's rejection of a column reachable within a single root via more than
	/// one selector condition (OR-activation is unsupported), so the config load fails with a clean
	/// aggregated error instead of throwing later at registry construction. The resolver keeps its
	/// own guard as defense-in-depth. For each root the column-key map is fresh, so the same column
	/// shared across DIFFERENT roots is allowed and not flagged.
	/// </summary>
	private static void ValidateNoSharedColumnAcrossBranches(
		List<ActionDto> actions,
		Dictionary<int, ActionDto> actionsById,
		List<Result> validationResults)
	{
		foreach (var root in actions)
		{
			if (IsSubaction(root))
			{
				continue;
			}

			var pathByColumnKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var onPath = new HashSet<int>();

			if (TryFindSharedColumn(
					root.Id,
					string.Empty,
					actionsById,
					pathByColumnKey,
					onPath,
					out var conflictingKey))
			{
				validationResults.Add(Result.Fail(
					$"[actions, Id={root.Id}] Column key '{conflictingKey}' is reachable within action "
					+ $"id {root.Id} via more than one selector condition (for example two selector "
					+ $"values mapping to the same subaction, or two different selectors reaching it). "
					+ $"A column may be activated by only a single condition path; OR-activation "
					+ $"across branches is not supported"));
			}
		}
	}

	/// <summary>
	/// Depth-first walk of the <c>targets</c> graph from one root, tracking per column key the
	/// activation-path signature (the ordered selector edges taken to reach it; the root's own
	/// columns have an empty signature = always-active). Returns <c>true</c> with the conflicting
	/// key the first time a column key is reached with a path signature different from the one first
	/// seen (OR-activation, which is unsupported), stopping the walk at most one finding per root.
	/// Cycle-safe via <paramref name="onPath"/> (cycles are reported separately by
	/// <see cref="ValidateNoCycles"/>).
	/// </summary>
	private static bool TryFindSharedColumn(
		int actionId,
		string pathSignature,
		Dictionary<int, ActionDto> actionsById,
		Dictionary<string, string> pathByColumnKey,
		HashSet<int> onPath,
		out string conflictingKey)
	{
		conflictingKey = string.Empty;

		if (!onPath.Add(actionId))
		{
			return false;
		}

		try
		{
			if (!actionsById.TryGetValue(actionId, out var action) || action.Columns == null)
			{
				return false;
			}

			foreach (var column in action.Columns)
			{
				if (string.IsNullOrEmpty(column.Key))
				{
					continue;
				}

				if (pathByColumnKey.TryGetValue(column.Key, out var seenSignature))
				{
					if (!string.Equals(seenSignature, pathSignature, StringComparison.Ordinal))
					{
						conflictingKey = column.Key;
						return true;
					}
				}
				else
				{
					pathByColumnKey[column.Key] = pathSignature;
				}

				if (column.Targets == null || column.Targets.Count == 0)
				{
					continue;
				}

				foreach (var (selectorValue, targetId) in column.Targets)
				{
					var childSignature = $"{pathSignature}|{column.Key}={selectorValue}";

					if (TryFindSharedColumn(
							targetId,
							childSignature,
							actionsById,
							pathByColumnKey,
							onPath,
							out conflictingKey))
					{
						return true;
					}
				}
			}

			return false;
		}
		finally
		{
			onPath.Remove(actionId);
		}
	}

	/// <summary>
	/// Surfaces a reference-graph cycle as a clean config-load validation error, consistent with
	/// the sibling dangling/orphan rules. The resolver also guards against cycles defensively, but
	/// reporting it here means the config load fails with all reference errors together rather than
	/// throwing later at registry construction.
	/// </summary>
	private static void ValidateNoCycles(
		List<ActionDto> actions,
		Dictionary<int, ActionDto> actionsById,
		List<Result> validationResults)
	{
		var fullyExplored = new HashSet<int>();
		var reported = false;

		foreach (var action in actions)
		{
			if (reported)
			{
				break;
			}

			var onPath = new HashSet<int>();
			if (HasCycle(action.Id, actionsById, fullyExplored, onPath))
			{
				validationResults.Add(Result.Fail(
					$"[actions, Id={action.Id}] Cycle detected in the action reference graph reachable "
					+ $"from action id {action.Id}"));
				reported = true;
			}
		}
	}

	private static bool HasCycle(
		int actionId,
		Dictionary<int, ActionDto> actionsById,
		HashSet<int> fullyExplored,
		HashSet<int> onPath)
	{
		if (fullyExplored.Contains(actionId))
		{
			return false;
		}

		if (!onPath.Add(actionId))
		{
			return true;
		}

		if (actionsById.TryGetValue(actionId, out var action) && action.Columns != null)
		{
			foreach (var column in action.Columns)
			{
				if (column.Targets == null)
				{
					continue;
				}

				foreach (var targetId in column.Targets.Values)
				{
					if (HasCycle(targetId, actionsById, fullyExplored, onPath))
					{
						return true;
					}
				}
			}
		}

		onPath.Remove(actionId);
		fullyExplored.Add(actionId);
		return false;
	}

	private static bool IsSubaction(ActionDto action)
	{
		return string.Equals(action.Role, "subaction", StringComparison.OrdinalIgnoreCase);
	}

	private static void ValidateColumnReferences(
		List<ColumnDto> columns,
		HashSet<string> propertyIds,
		List<Result> validationResults)
	{
		foreach (var column in columns)
		{
			if (column.BusinessLogic == null)
			{
				continue;
			}

			var propertyTypeId = column.BusinessLogic.PropertyTypeId;
			if (string.IsNullOrEmpty(propertyTypeId))
			{
				continue;
			}

			if (!propertyIds.Contains(propertyTypeId))
			{
				validationResults.Add(Result.Fail(
					$"[columns, Key='{column.Key}'] Column '{column.Key}' references unknown property_type_id: '{propertyTypeId}'"));
			}
		}
	}

	private static void ValidateActionReferences(
		List<ActionDto> actions,
		HashSet<string> propertyIds,
		HashSet<string> columnKeys,
		HashSet<string> groupIds,
		List<Result> validationResults)
	{
		foreach (var action in actions)
		{
			if (action.Columns == null)
			{
				continue;
			}

			var actionLocation = $"actions, Id={action.Id}, UiName='{action.UiName}'";

			foreach (var column in action.Columns)
			{
				if (string.IsNullOrEmpty(column.Key))
				{
					continue;
				}

				if (!columnKeys.Contains(column.Key))
				{
					validationResults.Add(Result.Fail(
						$"[{actionLocation}] Action '{action.UiName}' references unknown column: '{column.Key}'"));
				}

				if (!string.IsNullOrEmpty(column.PropertyTypeId) && !propertyIds.Contains(column.PropertyTypeId))
				{
					validationResults.Add(Result.Fail(
						$"[{actionLocation}] Action '{action.UiName}' column '{column.Key}' references unknown property_type_id: '{column.PropertyTypeId}'"));
				}

				if (!string.IsNullOrEmpty(column.GroupName) && !groupIds.Contains(column.GroupName))
				{
					validationResults.Add(Result.Fail(
						$"[{actionLocation}] Action '{action.UiName}' column '{column.Key}' references unknown group_name: '{column.GroupName}'"));
				}

				if (string.Equals(column.PropertyTypeId, "enum", StringComparison.OrdinalIgnoreCase)
					&& string.IsNullOrEmpty(column.GroupName))
				{
					validationResults.Add(Result.Fail(
						$"[{actionLocation}] Action '{action.UiName}' column '{column.Key}' has property_type_id 'enum' but no group_name specified"));
				}
			}
		}
	}
}
