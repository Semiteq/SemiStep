using TypesShared.Config;
using TypesShared.Core;

namespace Domain.Helpers;

internal static class CellStateResolver
{
	public static CellState GetCellState(GridColumnDefinition column, ActionDefinition action)
	{
		if (column.Key is "action")
		{
			return CellState.Enabled;
		}

		if (column.ColumnType is "step_start_time_field")
		{
			return CellState.Readonly;
		}

		if (!IsPropertyPresentInAction(column.Key, action))
		{
			return CellState.Disabled;
		}

		if (column.ReadOnly)
		{
			return CellState.Readonly;
		}

		return CellState.Enabled;
	}

	private static bool IsPropertyPresentInAction(string columnKey, ActionDefinition action)
	{
		return action.Properties.Any(col => col.Key == columnKey);
	}
}
