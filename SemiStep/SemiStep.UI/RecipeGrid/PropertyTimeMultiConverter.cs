using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal sealed class PropertyTimeMultiConverter : IMultiValueConverter
{
	public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count < 3)
		{
			return string.Empty;
		}

		if (values.Any(v => v == AvaloniaProperty.UnsetValue))
		{
			return string.Empty;
		}

		var cellValue = values[0];
		var units = values[1] as string;
		var formatKind = values[2] as string;

		if (cellValue is null)
		{
			return string.Empty;
		}

		var rawString = cellValue.ToString();
		if (string.IsNullOrEmpty(rawString))
		{
			return string.Empty;
		}

		return TimeFormatHelper.FormatValue(rawString, formatKind, units);
	}
}
