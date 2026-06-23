namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Describes a selector-column edit emitted by <see cref="RecipeRowViewModel"/>: the selector value
/// change together with the columns its new selection deactivates (<see cref="ColumnsToDrop"/>) and
/// activates (<see cref="ColumnsToSeed"/>, the keys to seed; the core mutation resolves each column's
/// default value). The grid view-model routes this to the batched core mutation so the whole
/// composition change is one undo unit.
/// </summary>
public sealed record SelectorEdit(
	string SelectorKey,
	string Value,
	IReadOnlyCollection<string> ColumnsToDrop,
	IReadOnlyCollection<string> ColumnsToSeed);
