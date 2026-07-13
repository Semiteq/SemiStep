namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Cross-surface channel for the click-away clear of a changed (orange) cell. Both orientation
/// surfaces are live and hold their own row view model for the same step, and a click-away is a
/// pure UI acknowledgement — it fires no recipe mutation for <see cref="RecipeRowUpdateSynchronizer"/>
/// to derive the clear from. The surface that resolved the click-away publishes the step/column
/// pair here, and every subscribed surface (the originator included) clears its own row.
/// </summary>
public sealed class ChangedCellClickAwayBroadcaster
{
	public event Action<int, string>? Cleared;

	public void Publish(int stepIndex, string columnKey)
	{
		Cleared?.Invoke(stepIndex, columnKey);
	}
}
