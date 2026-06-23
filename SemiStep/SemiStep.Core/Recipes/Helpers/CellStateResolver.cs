namespace SemiStep.Core.Recipes.Helpers;

public static class CellStateResolver
{
	/// <summary>
	/// Reports whether this cell should be painted as row-level inapplicable.
	/// False for the action column and for columns whose <see cref="GridColumnDefinition.ReadOnly"/> flag is set —
	/// those are handled separately. Otherwise, true iff the column is not in the step's active set:
	/// either the action does not define this column, or its activation conditions are unmet by the
	/// step's current selector values.
	/// </summary>
	/// <remarks>
	/// Invariant: the "read-only column" and "inapplicable" signals are disjoint by design — never both true for the same cell.
	/// </remarks>
	public static bool IsInapplicable(GridColumnDefinition column, IReadOnlySet<string> activeColumnKeys)
	{
		ArgumentNullException.ThrowIfNull(activeColumnKeys);

		return column.Key != StepValueParser.ActionColumnKey
			&& !column.ReadOnly
			&& !activeColumnKeys.Contains(column.Key);
	}
}
