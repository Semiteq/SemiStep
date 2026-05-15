namespace SemiStep.UI.RecipeGrid;

internal static class ColumnTypes
{
	public const string Action = "action";
	public const string ActionComboBox = "action_combo_box";
	public const string ActionTargetComboBox = "action_target_combo_box";
	public const string PropertyField = "property_field";
	public const string StepStartTimeField = "step_start_time_field";
	public const string TextField = "text_field";

	public static bool IsGroupBoundColumn(string columnType)
	{
		return string.Equals(columnType, ActionTargetComboBox, StringComparison.OrdinalIgnoreCase);
	}

	public static string IndexerPath(string columnKey)
	{
		return $"[{columnKey}]";
	}

	public static string CellStatePath(string columnKey)
	{
		return $"CellStates[{columnKey}]";
	}

	public static string GroupItemsPath(string columnKey)
	{
		return $"GroupItemsByColumn[{columnKey}]";
	}
}
