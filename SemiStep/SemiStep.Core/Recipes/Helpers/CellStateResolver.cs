namespace SemiStep.Core.Recipes.Helpers;

public static class CellStateResolver
{
	/// <summary>
	/// Reports whether this cell should be painted as row-level inapplicable.
	/// False for the action column and for columns whose <see cref="GridColumnDefinition.ReadOnly"/> flag is set —
	/// those are handled separately. Otherwise, true iff the action does not define this column's property.
	/// </summary>
	/// <remarks>
	/// Invariant: the "read-only column" and "inapplicable" signals are disjoint by design — never both true for the same cell.
	/// </remarks>
	public static bool IsInapplicable(GridColumnDefinition column, ActionDefinition action)
	{
		return column.Key != StepValueParser.ActionColumnKey
			&& !column.ReadOnly
			&& !IsPropertyPresentInAction(column.Key, action);
	}

	private static bool IsPropertyPresentInAction(string columnKey, ActionDefinition action)
	{
		return action.Properties.Any(col => col.Key == columnKey);
	}
}
