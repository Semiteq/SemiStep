namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Cross-surface channel that clears one change mark on both orientation surfaces, after a click-away or an
/// edit of the cell; each surface holds its own row for the step, and neither clear is a recipe mutation.
/// </summary>
public sealed class ChangeMarkClearBroadcaster
{
	public event Action<int, string>? Cleared;

	public void Publish(int stepIndex, string columnKey)
	{
		Cleared?.Invoke(stepIndex, columnKey);
	}
}
