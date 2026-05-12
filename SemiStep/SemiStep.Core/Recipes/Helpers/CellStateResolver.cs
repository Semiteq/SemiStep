namespace SemiStep.Core.Recipes.Helpers;

public static class CellStateResolver
{
	public static CellState GetCellState(GridColumnDefinition column, ActionDefinition action)
	{
		if (column.Key == StepValueParser.ActionColumnKey)
		{
			return CellState.Enabled;
		}

		if (column.ReadOnly)
		{
			return CellState.Readonly;
		}

		if (!IsPropertyPresentInAction(column.Key, action))
		{
			return CellState.Disabled;
		}

		return CellState.Enabled;
	}

	private static bool IsPropertyPresentInAction(string columnKey, ActionDefinition action)
	{
		return action.Properties.Any(col => col.Key == columnKey);
	}
}
