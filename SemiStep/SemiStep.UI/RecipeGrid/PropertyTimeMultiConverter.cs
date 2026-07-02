using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal sealed class PropertyTimeMultiConverter : IMultiValueConverter
{
	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count < 3)
		{
			return string.Empty;
		}

		for (var i = 0; i < values.Count; i++)
		{
			if (values[i] == AvaloniaProperty.UnsetValue)
			{
				return string.Empty;
			}
		}

		var cellValue = values[0];
		var units = values[1] as string;
		var formatKind = values[2] as string;

		if (cellValue is null)
		{
			return string.Empty;
		}

		var rawString = cellValue as string ?? FormatNumeric(cellValue);
		if (string.IsNullOrEmpty(rawString))
		{
			return string.Empty;
		}

		return TimeFormatHelper.FormatValue(rawString, formatKind, units);
	}

	internal static string FormatNumeric(object cellValue)
	{
		if (cellValue is float or double or decimal)
		{
			return ((IFormattable)cellValue).ToString("0.###", CultureInfo.InvariantCulture);
		}

		return cellValue.ToString() ?? string.Empty;
	}
}
