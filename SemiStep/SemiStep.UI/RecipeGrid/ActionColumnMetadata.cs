using System.Runtime.CompilerServices;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

/// <summary>An action's column units, format kinds and group items, built once per action definition.</summary>
internal sealed record ActionColumnMetadata(
	IReadOnlyDictionary<string, string?> Units,
	IReadOnlyDictionary<string, string> FormatKinds,
	IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> GroupItemsByColumn)
{
	// Cached per action (stable ActionDefinition instance makes the reference key safe); weak table drops entries when an action is collected.
	private static readonly ConditionalWeakTable<ActionDefinition, ActionColumnMetadata> _cache = [];

	public static ActionColumnMetadata For(
		ActionDefinition action,
		RecipeMetadataRegistry recipeMetadataRegistry,
		ComboBoxItemsProvider comboBoxItemsProvider)
	{
		return _cache.GetValue(
			action,
			resolvedAction =>
			{
				var (units, formatKinds) = BuildColumnMetadata(resolvedAction, recipeMetadataRegistry);
				return new ActionColumnMetadata(
					units,
					formatKinds,
					BuildGroupItemsByColumn(resolvedAction, recipeMetadataRegistry, comboBoxItemsProvider));
			});
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> BuildGroupItemsByColumn(
		ActionDefinition actionDefinition,
		RecipeMetadataRegistry recipeMetadataRegistry,
		ComboBoxItemsProvider comboBoxItemsProvider)
	{
		// Pre-populate group-bound columns with empty lists so a recycle onto an action lacking the property doesn't throw KeyNotFoundException.
		var result = new Dictionary<string, IReadOnlyList<ComboBoxItemViewModel>>(StringComparer.OrdinalIgnoreCase);

		foreach (var column in recipeMetadataRegistry.GetAllColumns())
		{
			if (ColumnTypes.IsGroupBoundColumn(column.ColumnType))
			{
				result[column.Key] = [];
			}
		}

		foreach (var actionProperty in actionDefinition.Properties)
		{
			if (actionProperty.GroupName is null)
			{
				continue;
			}

			// Validation already rejects unknown keys; this guard confines the overlay to pre-populated columns.
			if (!result.ContainsKey(actionProperty.Key))
			{
				continue;
			}

			result[actionProperty.Key] = comboBoxItemsProvider.GetGroupItems(actionProperty.GroupName);
		}

		return result;
	}

	private static (IReadOnlyDictionary<string, string?> Units, IReadOnlyDictionary<string, string> FormatKinds) BuildColumnMetadata(
		ActionDefinition actionDefinition,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var units = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		var formatKinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (var actionProperty in actionDefinition.Properties)
		{
			var propertyType = ResolvePropertyType(actionProperty, recipeMetadataRegistry);
			units[actionProperty.Key] = propertyType?.Units;
			formatKinds[actionProperty.Key] = propertyType?.FormatKind ?? TimeFormatHelper.DefaultFormatKind;
		}

		formatKinds[TimeFormatHelper.StepStartTimeColumnKey] = TimeFormatHelper.TimeHmsFormat;

		return (units, formatKinds);
	}

	private static PropertyTypeDefinition? ResolvePropertyType(
		ActionPropertyDefinition actionProperty,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var propertyResult = recipeMetadataRegistry.GetProperty(actionProperty.PropertyTypeId);
		return propertyResult.IsSuccess ? propertyResult.Value : null;
	}
}
