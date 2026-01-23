using Config.Models;

namespace Config.Validation;

public sealed class CrossReferenceValidator
{
	public static ConfigContext Validate(ConfigContext context)
	{
		if (context.HasErrors)
		{
			return context;
		}

		if (context.Properties == null
			|| context.Columns == null
			|| context.Groups == null
			|| context.Actions == null)
		{
			context.AddError("Cannot perform cross-reference validation: some sections are not loaded");
			return context;
		}

		var propertyIds = context.Properties
			.Where(p => !string.IsNullOrEmpty(p.PropertyTypeId))
			.Select(p => p.PropertyTypeId!)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var columnKeys = context.Columns
			.Where(c => !string.IsNullOrEmpty(c.Key))
			.Select(c => c.Key!)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var groupIds = context.Groups.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

		ValidateColumnReferences(context, propertyIds);
		ValidateActionReferences(context, propertyIds, columnKeys, groupIds);

		return context;
	}

	private static void ValidateColumnReferences(ConfigContext context, HashSet<string> propertyIds)
	{
		foreach (var column in context.Columns!)
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
				context.AddError(
					$"Column '{column.Key}' references unknown property_type_id: '{propertyTypeId}'",
					$"columns, Key='{column.Key}'");
			}
		}
	}

	private static void ValidateActionReferences(
		ConfigContext context,
		HashSet<string> propertyIds,
		HashSet<string> columnKeys,
		HashSet<string> groupIds)
	{
		foreach (var action in context.Actions!)
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
					context.AddError(
						$"Action '{action.UiName}' references unknown column: '{column.Key}'",
						actionLocation);
				}

				if (!string.IsNullOrEmpty(column.PropertyTypeId) && !propertyIds.Contains(column.PropertyTypeId))
				{
					context.AddError(
						$"Action '{action.UiName}' column '{column.Key}' references unknown property_type_id: '{column.PropertyTypeId}'",
						actionLocation);
				}

				if (!string.IsNullOrEmpty(column.GroupName) && !groupIds.Contains(column.GroupName))
				{
					context.AddError(
						$"Action '{action.UiName}' column '{column.Key}' references unknown group_name: '{column.GroupName}'",
						actionLocation);
				}
			}
		}
	}
}
