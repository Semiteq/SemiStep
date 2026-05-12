using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal sealed class PropertyTimeEditingConverter(string formatKind) : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is null)
		{
			return string.Empty;
		}

		var rawString = value.ToString();
		if (string.IsNullOrEmpty(rawString))
		{
			return string.Empty;
		}

		return TimeFormatHelper.FormatValue(rawString, formatKind, units: null);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var text = value?.ToString()?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return BindingOperations.DoNothing;
		}

		var parsed = TimeFormatHelper.ParseValue(text);

		if (parsed == text && text.Contains(':'))
		{
			return BindingOperations.DoNothing;
		}

		return parsed;
	}
}
