using Avalonia.Controls;
using Avalonia.Input;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedGridSelectionController(ListBox stepListBox)
{
	// Returns true when NOT consumed (second press on the already-selected column) so the caller routes to edit.
	public bool HandleCellSelectionPress(
		TransposedRecipeGridSurface? surface,
		ParameterCellViewModel pressedCell,
		PointerPressedEventArgs e)
	{
		if (surface is null)
		{
			return false;
		}

		var index = TransposedGridCellLocator.IndexOfColumn(surface, pressedCell.Row);
		if (index < 0)
		{
			return false;
		}

		if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
		{
			ToggleColumnSelection(surface, index);
		}
		else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
		{
			ExtendSelectionTo(surface, index);
		}
		else if (IsSingleSelectedColumn(index))
		{
			return true;
		}
		else
		{
			stepListBox.SelectedIndex = index;
		}

		// Taking over the press must still commit a pending edit elsewhere: move focus to the
		// pressed container so the previous editor's LostFocus fires.
		(stepListBox.ContainerFromIndex(index) as Control)?.Focus();
		e.Handled = true;

		return false;
	}

	private bool IsSingleSelectedColumn(int index)
	{
		return stepListBox.SelectedItems is { Count: 1 } && stepListBox.SelectedIndex == index;
	}

	private void ToggleColumnSelection(TransposedRecipeGridSurface surface, int index)
	{
		if (stepListBox.SelectedItems is not { } selectedItems)
		{
			return;
		}

		var item = surface.StepColumns[index];
		if (selectedItems.Contains(item))
		{
			selectedItems.Remove(item);
		}
		else
		{
			selectedItems.Add(item);
		}
	}

	private void ExtendSelectionTo(TransposedRecipeGridSurface surface, int index)
	{
		if (stepListBox.SelectedItems is not { } selectedItems)
		{
			return;
		}

		var anchor = stepListBox.Selection.AnchorIndex;
		if (anchor < 0)
		{
			anchor = index;
		}

		selectedItems.Clear();
		for (var i = Math.Min(anchor, index); i <= Math.Max(anchor, index); i++)
		{
			selectedItems.Add(surface.StepColumns[i]);
		}
	}
}
