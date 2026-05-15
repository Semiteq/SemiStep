using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

public sealed class CellStateToBoolConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is CellState cellState)
		{
			return cellState == CellState.Enabled;
		}

		return false;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return BindingOperations.DoNothing;
	}
}
