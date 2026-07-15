using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Resolves a fixed column-cell slot to Cells[index] so a recycled container rebinds slot i from the
// old column's cell to the new column's cell on a single DataContext change, instead of rebuilding.
// The slot count and order are constant across columns (one cell per ParameterDescriptor), so the
// index is baked once per slot and passed as the converter parameter.
internal sealed class CellSlotConverter : IValueConverter
{
	public static readonly CellSlotConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is IReadOnlyList<ParameterCellViewModel> cells
			&& parameter is int index
			&& index >= 0
			&& index < cells.Count)
		{
			return cells[index];
		}

		return AvaloniaProperty.UnsetValue;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return AvaloniaProperty.UnsetValue;
	}
}
