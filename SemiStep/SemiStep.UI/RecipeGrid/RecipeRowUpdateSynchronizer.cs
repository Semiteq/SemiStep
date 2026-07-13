using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Applies a PropertyUpdated mutation to a row view model. Both orientation surfaces are live
/// and subscribed to the coordinator, but only the originating surface's edit handlers adjust
/// applicability and the changed-cell set; this helper derives the same adjustments from the
/// old-vs-new step delta so the sibling surface's row stays in sync (see
/// Docs/architecture/cell-change-highlight.md):
/// a value change is a successful edit of that cell (clears its changed flag), an added key is
/// a selector-seeded column (marks it changed), a removed key is a selector-dropped column
/// (unmarks it), and any selector change shifts which columns are applicable.
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
		List<string>? seededKeys = null;
		List<string>? droppedKeys = null;

		foreach (var (key, value) in newStep.Properties)
		{
			if (!oldStep.Properties.TryGetValue(key, out var oldValue))
			{
				(seededKeys ??= []).Add(key.Value);
			}
			else if (!oldValue.Equals(value))
			{
				row.ClearChanged(key.Value);
			}
		}

		foreach (var key in oldStep.Properties.Keys)
		{
			if (!newStep.Properties.ContainsKey(key))
			{
				(droppedKeys ??= []).Add(key.Value);
			}
		}

		if (seededKeys is not null || droppedKeys is not null)
		{
			row.ApplyChangedDelta(add: seededKeys ?? [], remove: droppedKeys ?? []);
		}
	}
}
