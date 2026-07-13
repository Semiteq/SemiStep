namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Pure decision for the changed-cell click-away rule: given the currently armed cell and the cell
/// just pressed, decides which cell (if any) must lose its orange highlight and which cell becomes the
/// new armed cell. Kept free of Avalonia event types so the branching can be unit-tested directly; the
/// view handlers (canonical and transposed) only resolve the pressed row/column from the event and
/// route the clear through their surface's <c>ClearChangedByClickAway</c>, which broadcasts it to
/// both orientation surfaces and applies the still-in-grid guard.
/// </summary>
internal static class ChangedCellClickResolver
{
	internal static (
		(RecipeRowViewModel Row, string ColumnKey)? CellToClear,
		(RecipeRowViewModel Row, string ColumnKey)? NewPending) Resolve(
		(RecipeRowViewModel Row, string ColumnKey)? pending,
		RecipeRowViewModel? pressedRow,
		string? pressedColumnKey)
	{
		var newPending = pressedRow is not null
			&& pressedColumnKey is not null
			&& pressedRow.IsChanged(pressedColumnKey)
				? (pressedRow, pressedColumnKey)
				: ((RecipeRowViewModel Row, string ColumnKey)?)null;

		if (pending is not { } current)
		{
			return (null, newPending);
		}

		var pressedSameAsPending = pressedRow is not null
			&& pressedColumnKey is not null
			&& ReferenceEquals(current.Row, pressedRow)
			&& string.Equals(current.ColumnKey, pressedColumnKey, StringComparison.OrdinalIgnoreCase);

		return (pressedSameAsPending ? null : current, newPending);
	}
}
