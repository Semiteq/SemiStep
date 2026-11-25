using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Applies a PropertyUpdated mutation to a row on either surface: every key whose value changed is marked
/// (<see cref="RecipeRowViewModel.ChangedValueKeys"/>), a removed key is unmarked. The edited cell's
/// mark is cleared afterwards through <see cref="ChangeMarkClearBroadcaster"/>.
/// </summary>
internal static class RecipeRowUpdateSynchronizer
{
	public static void ApplyPropertyUpdate(RecipeRowViewModel row, Step newStep)
	{
		var oldStep = row.CurrentStep;

		row.UpdateStep(newStep);
		row.RecomputeInapplicableColumns();
		SyncChangedColumns(row, oldStep, newStep);
	}

	private static void SyncChangedColumns(RecipeRowViewModel row, Step oldStep, Step newStep)
	{
		var changedKeys = RecipeRowViewModel.ChangedValueKeys(oldStep, newStep);
		var droppedKeys = oldStep.Properties.Keys
			.Where(key => !newStep.Properties.ContainsKey(key))
			.Select(key => key.Value)
			.ToList();

		if (changedKeys.Count > 0 || droppedKeys.Count > 0)
		{
			row.ApplyChangedDelta(add: changedKeys, remove: droppedKeys);
		}
	}
}
