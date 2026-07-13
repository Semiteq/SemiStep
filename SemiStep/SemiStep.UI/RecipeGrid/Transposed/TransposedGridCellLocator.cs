using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal static class TransposedGridCellLocator
{
	// Walks the visual tree upward from the press/focus source until a cell's DataContext is
	// found or the step ListBox itself is reached (header/marker presses resolve no cell).
	public static ParameterCellViewModel? ResolveCell(Visual source, ListBox stepListBox)
	{
		var current = source;
		while (current is not null && !ReferenceEquals(current, stepListBox))
		{
			if (current is Control { DataContext: ParameterCellViewModel cell })
			{
				return cell;
			}

			current = current.GetVisualParent();
		}

		return null;
	}

	public static int IndexOfColumn(TransposedRecipeGridSurface surface, RecipeRowViewModel row)
	{
		for (var i = 0; i < surface.StepColumns.Count; i++)
		{
			if (ReferenceEquals(surface.StepColumns[i].Row, row))
			{
				return i;
			}
		}

		return -1;
	}
}
