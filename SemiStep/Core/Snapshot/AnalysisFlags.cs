namespace Core.Snapshot;

/// <summary>
/// Flags derived during analysis influencing validity.
/// </summary>
public readonly struct AnalysisFlags
{
	public bool EmptyRecipe { get; init; }
	public bool LoopIntegrityCompromised { get; init; }
	public bool MaxDepthExceeded { get; init; }

	public AnalysisFlags(bool emptyRecipe, bool loopIntegrityCompromised, bool maxDepthExceeded)
	{
		EmptyRecipe = emptyRecipe;
		LoopIntegrityCompromised = loopIntegrityCompromised;
		MaxDepthExceeded = maxDepthExceeded;
	}

	public static AnalysisFlags None => new(false, false, false);
}
