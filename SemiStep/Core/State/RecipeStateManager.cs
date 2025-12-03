using Core.Snapshot;

namespace Core.State;

/// <summary>
/// Maintains current and last valid snapshots.
/// Thread-safe via interlocked exchange.
/// </summary>
public sealed class RecipeStateManager : IRecipeStateManager
{
	private RecipeAnalysisSnapshot _current = RecipeAnalysisSnapshot.Empty;
	private RecipeAnalysisSnapshot? _lastValid;

	public RecipeAnalysisSnapshot Current => _current;
	public RecipeAnalysisSnapshot? LastValid => _lastValid;

	public void Update(RecipeAnalysisSnapshot snapshot)
	{
		Interlocked.Exchange(ref _current, snapshot);
		if (snapshot.IsValid)
		{
			Interlocked.Exchange(ref _lastValid, snapshot);
		}
	}
}
