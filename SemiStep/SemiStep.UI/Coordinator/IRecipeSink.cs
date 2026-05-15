namespace SemiStep.UI.Coordinator;

/// <summary>
/// Receives mutation signals from <see cref="RecipeCoordinator"/>. Implementations must
/// process each signal synchronously and are guaranteed to be invoked on the Avalonia UI
/// dispatcher thread. A single coordinator owns at most one sink — see
/// <see cref="RecipeCoordinator.Attach"/>.
/// </summary>
/// <remarks>
/// An exception thrown by <see cref="OnMutation"/> is caught by the dispatch pipeline,
/// logged, and does not abort subsequent <c>Mutated</c> event invocations. Implementations
/// should still keep <see cref="OnMutation"/> defensive: an unhandled exception leaves the
/// sink and the coordinator in an inconsistent state, even though it will not tear down
/// other subscribers.
/// </remarks>
public interface IRecipeSink
{
	void OnMutation(MutationSignal signal);
}
