namespace SemiStep.Core.Recipes;

/// <summary>
/// Successful structural mutations on <see cref="RecipeSession"/> return the index the UI
/// should select next (e.g. the inserted row, or the row that took the place of a removed
/// one). <c>null</c> means leave the current selection unchanged.
/// </summary>
public readonly record struct MutationOutcome(int? SuggestedSelectionIndex);
