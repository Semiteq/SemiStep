namespace SemiStep.Core.Recipes.Helpers;

public static class CellStateResolver
{
	public static bool IsInapplicable(GridColumnDefinition column, ActionDefinition action)
	{
		if (column.Key == StepValueParser.ActionColumnKey)
		{
			return false;
		}

		if (column.ReadOnly)
		{
			return false;
		}

		return !IsPropertyPresentInAction(column.Key, action);
	}

	private static bool IsPropertyPresentInAction(string columnKey, ActionDefinition action)
	{
		return action.Properties.Any(col => col.Key == columnKey);
	}
}
