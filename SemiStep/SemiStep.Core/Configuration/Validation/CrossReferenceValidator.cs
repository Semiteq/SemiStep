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
