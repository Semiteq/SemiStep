using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal sealed class PropertyTimeEditingConverter(string formatKind, bool allowsEmpty) : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return FormatForDisplay(value, formatKind);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return ParseForCommit(value, allowsEmpty);
	}

	// Units-less display formatting shared with the transposed recyclable text editor, which drives
	// the same rendering through a stateless OneWay MultiBinding instead of a per-cell converter.
	public static string FormatForDisplay(object? value, string? formatKind)
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

	// Commit-side parse shared with the transposed recyclable text editor, which owns the write in
	// its LostFocus/KeyDown handlers (a MultiBinding has no ConvertBack). Returns
	// BindingOperations.DoNothing when the edit must be rejected without touching the model.
	public static object? ParseForCommit(object? value, bool allowsEmpty)
	{
		var text = value?.ToString()?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return allowsEmpty ? string.Empty : BindingOperations.DoNothing;
		}

		var parsed = TimeFormatHelper.ParseValue(text);

		if (parsed == text && text.Contains(':'))
		{
			return BindingOperations.DoNothing;
		}

		return parsed;
	}
}
