using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SemiStep.UI.RecipeGrid;

internal static class RecipeRowExecutionClassBinder
{
	// Avalonia 12 BindClass: the lifecycle is owned by the DataGridRow's value store.
	// On row recycling (new DataContext bound to the same container) the binding
	// re-evaluates against the new DataContext automatically. On row detach (UnloadingRow)
	// the binding tears down with the row, so no manual cleanup or bookkeeping is needed.
	public static void BindAll(DataGridRow row)
	{
		row.BindClass(
			RowExecutionClasses.CurrentStepClass,
			new Binding(nameof(RecipeRowViewModel.IsCurrentStep)),
			row);
		row.BindClass(
			RowExecutionClasses.PastStepClass,
			new Binding(nameof(RecipeRowViewModel.IsPastStep)),
			row);
		row.BindClass(
			RowExecutionClasses.ForDepth1Class,
			new Binding(nameof(RecipeRowViewModel.IsForDepth1)),
			row);
		row.BindClass(
			RowExecutionClasses.ForDepth2Class,
			new Binding(nameof(RecipeRowViewModel.IsForDepth2)),
			row);
		row.BindClass(
			RowExecutionClasses.ForDepth3Class,
			new Binding(nameof(RecipeRowViewModel.IsForDepth3)),
			row);
	}
}
