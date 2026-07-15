using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid.Transposed;

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
