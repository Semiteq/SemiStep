using Avalonia.Controls;

namespace SemiStep.UI.RecipeGrid;

internal static class RowExecutionClasses
{
	public const string CurrentStepClass = "current-step";
	public const string PastStepClass = "past-step";

	public static void Apply(DataGridRow dataGridRow, RecipeRowViewModel row)
	{
		ToggleClass(dataGridRow, CurrentStepClass, row.IsCurrentStep);
		ToggleClass(dataGridRow, PastStepClass, row.IsPastStep);
	}

	public static void Clear(DataGridRow dataGridRow)
	{
		dataGridRow.Classes.Remove(CurrentStepClass);
		dataGridRow.Classes.Remove(PastStepClass);
	}

	private static void ToggleClass(DataGridRow dataGridRow, string className, bool enabled)
	{
		if (enabled)
		{
			if (!dataGridRow.Classes.Contains(className))
			{
				dataGridRow.Classes.Add(className);
			}
		}
		else
		{
			dataGridRow.Classes.Remove(className);
		}
	}
}
