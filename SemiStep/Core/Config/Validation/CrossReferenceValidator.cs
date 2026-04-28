using Config.Dto;

using FluentResults;

namespace Config.Validation;

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

		if (validationResults.Count == 0)
		{
			return Result.Ok();
		}

		return Result.Merge(validationResults.ToArray());
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
