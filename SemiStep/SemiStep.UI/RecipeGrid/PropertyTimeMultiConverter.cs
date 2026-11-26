using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

/// <summary>(value, format kind) to the cell text without a unit; the unit has its own slot.</summary>
internal sealed class PropertyTimeMultiConverter : IMultiValueConverter
{
	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count < 2)
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
		var formatKind = values[1] as string;

		if (cellValue is null)
		{
			return string.Empty;
		}

		var rawString = cellValue as string ?? FormatNumeric(cellValue);
		return string.IsNullOrEmpty(rawString) ? string.Empty : TimeFormatHelper.FormatValue(rawString, formatKind, units: null);
	}

	internal static string FormatNumeric(object cellValue)
	{
		return cellValue is float or double or decimal
			? ((IFormattable)cellValue).ToString("0.###", CultureInfo.InvariantCulture)
			: cellValue.ToString() ?? string.Empty;
	}
}
