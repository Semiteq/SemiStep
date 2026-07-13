namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Raised when a projected step's action key has no definition in the metadata registry.
/// Derives from <see cref="InvalidOperationException"/> so <c>Initialize()</c> callers observe
/// the standard invariant-breach type; the surface's <c>OnMutation</c> catches it specifically.
/// </summary>
internal sealed class UnknownActionKeyException(string message) : InvalidOperationException(message);
