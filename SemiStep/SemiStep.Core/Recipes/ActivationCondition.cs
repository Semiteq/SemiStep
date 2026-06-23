namespace SemiStep.Core.Recipes;

/// <summary>
/// A single condition on a column's activation path: the column is active only when the
/// selector identified by <see cref="SelectorKey"/> currently holds <see cref="EnablingValue"/>.
/// A column carries one condition per selector on its path from the primary action; it is
/// active iff every condition is met. An empty/absent list means always active.
/// </summary>
public sealed record ActivationCondition(string SelectorKey, int EnablingValue);
