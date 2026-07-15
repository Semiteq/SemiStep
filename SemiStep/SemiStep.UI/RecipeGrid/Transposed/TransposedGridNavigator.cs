using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Operator mental model: Right = next step (column), Down = next parameter (cell below).
// Keys inside an open ComboBox dropdown and caret movement inside a focused TextBox keep
// their native meaning.
internal sealed class TransposedGridNavigator(ListBox stepListBox)
{
	public void HandleTunnelKeyDown(TransposedRecipeGridSurface? surface, KeyEventArgs e)
	{
		if (surface is null
			|| e.KeyModifiers != KeyModifiers.None
			|| e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down))
		{
			return;
		}

		if (TopLevel.GetTopLevel(stepListBox)?.FocusManager?.GetFocusedElement() is not Control focused)
		{
			return;
		}

		if (focused.FindLogicalAncestorOfType<ComboBox>(includeSelf: true) is { IsDropDownOpen: true })
		{
			return;
		}

		var caretOwnsHorizontalKeys = e.Key is Key.Left or Key.Right
			&& focused.FindAncestorOfType<TextBox>(includeSelf: true) is not null;
		if (caretOwnsHorizontalKeys)
		{
			return;
		}

		if (TransposedGridCellLocator.ResolveCell(focused, stepListBox) is not { } cell
			|| LocateCell(surface, cell) is not { } position)
		{
			HandleContainerNavigation(surface, focused, e);
			return;
		}

		// Consumed even when the move is an edge no-op: the key must never fall through to a
		// closed ComboBox, which would cycle the cell value.
		e.Handled = true;

		if (e.Key is Key.Left or Key.Right)
		{
			MoveToNeighborColumn(surface, position, e.Key == Key.Right ? 1 : -1);
		}
		else
		{
			MoveWithinColumn(surface, position, e.Key == Key.Down ? 1 : -1);
		}
	}

	// A column container holds focus after a horizontal move whose target row had no focusable
	// editor; arrows must not dead-end there.
	private void HandleContainerNavigation(TransposedRecipeGridSurface surface, Control focused, KeyEventArgs e)
	{
		if (focused.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { } container)
		{
			return;
		}

		var columnIndex = stepListBox.IndexFromContainer(container);
		if (columnIndex < 0)
		{
			return;
		}

		e.Handled = true;

		switch (e.Key)
		{
			case Key.Left or Key.Right:
				MoveToNeighborColumn(surface, (columnIndex, 0), e.Key == Key.Right ? 1 : -1);
				break;
			case Key.Down:
				MoveWithinColumn(surface, (columnIndex, -1), 1);
				break;
			case Key.Up:
				MoveWithinColumn(surface, (columnIndex, surface.StepColumns[columnIndex].Cells.Count), -1);
				break;
		}
	}

	private static (int ColumnIndex, int ParameterIndex)? LocateCell(
		TransposedRecipeGridSurface surface,
		ParameterCellViewModel cell)
	{
		var columnIndex = TransposedGridCellLocator.IndexOfColumn(surface, cell.Row);
		if (columnIndex < 0)
		{
			return null;
		}

		var cells = surface.StepColumns[columnIndex].Cells;
		for (var parameterIndex = 0; parameterIndex < cells.Count; parameterIndex++)
		{
			if (ReferenceEquals(cells[parameterIndex], cell))
			{
				return (columnIndex, parameterIndex);
			}
		}

		return null;
	}

	private void MoveToNeighborColumn(
		TransposedRecipeGridSurface surface,
		(int ColumnIndex, int ParameterIndex) position,
		int direction)
	{
		var targetColumnIndex = position.ColumnIndex + direction;
		if (targetColumnIndex < 0 || targetColumnIndex >= surface.StepColumns.Count)
		{
			return;
		}

		stepListBox.SelectedIndex = targetColumnIndex;
		stepListBox.ScrollIntoView(targetColumnIndex);
		stepListBox.UpdateLayout();

		var focusTarget = FindFocusableCellPresenter(surface, targetColumnIndex, position.ParameterIndex)
			?? stepListBox.ContainerFromIndex(targetColumnIndex);
		focusTarget?.Focus();
	}

	// Non-focusable rows (read-only or inapplicable cells) are skipped in the travel direction.
	private void MoveWithinColumn(
		TransposedRecipeGridSurface surface,
		(int ColumnIndex, int ParameterIndex) position,
		int direction)
	{
		var cellCount = surface.StepColumns[position.ColumnIndex].Cells.Count;
		for (var parameterIndex = position.ParameterIndex + direction;
			parameterIndex >= 0 && parameterIndex < cellCount;
			parameterIndex += direction)
		{
			if (FindFocusableCellPresenter(surface, position.ColumnIndex, parameterIndex) is { } presenter)
			{
				presenter.Focus();
				return;
			}
		}
	}

	// Arrow navigation traverses cells by focusing the lazy display presenters (property-text and combo
	// alike): the heavy editor is built only on edit entry, so navigation targets the display visual and a
	// focused display then enters edit on F2 (text also on a printable keystroke).
	private Control? FindFocusableCellPresenter(
		TransposedRecipeGridSurface surface,
		int columnIndex,
		int parameterIndex)
	{
		if (stepListBox.ContainerFromIndex(columnIndex) is not ListBoxItem container)
		{
			return null;
		}

		var cell = surface.StepColumns[columnIndex].Cells[parameterIndex];

		return container.GetVisualDescendants()
			.OfType<TransposedLazyCellPresenter>()
			.FirstOrDefault(presenter => ReferenceEquals(presenter.DataContext, cell)
				&& presenter.Focusable
				&& presenter.IsEffectivelyEnabled);
	}
}
