using Recipe.Entities;

using Shared.Entities;

namespace Domain.Services;

public sealed class CellStateResolver
{
	public CellState GetCellState(Step step, GridColumnDefinition column, ActionDefinition action)
	{
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
		return action.Columns.Any(col => col.Key == columnKey);
	}
}
