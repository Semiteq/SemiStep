using System.Globalization;

using Avalonia.Data.Converters;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

public sealed class CellStateConverter(string columnKey) : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not IReadOnlyDictionary<string, CellState> cellStates)
		{
			return CellState.Enabled;
		}

		return cellStates.GetValueOrDefault(columnKey, CellState.Enabled);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value;
	}
}
