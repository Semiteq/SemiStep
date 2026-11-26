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
		return ParseForCommit(value, formatKind, allowsEmpty);
	}

	// Units-less editor text shared with the transposed recyclable text editor, which drives the same
	// rendering through a stateless OneWay MultiBinding instead of a per-cell converter. Invariant, as the
	// model parses it: the current culture would show 90.5 as "90,5", which the commit then rejects.
	public static string FormatForDisplay(object? value, string? formatKind)
	{
		var rawString = value is IFormattable number
			? number.ToString(format: null, CultureInfo.InvariantCulture)
			: value?.ToString();
		return string.IsNullOrEmpty(rawString) ? string.Empty : TimeFormatHelper.FormatForEdit(rawString, formatKind);
	}

	// Commit-side parse shared with the transposed recyclable text editor, which owns the write in
	// its LostFocus/KeyDown handlers (a MultiBinding has no ConvertBack). Returns
	// BindingOperations.DoNothing when the edit must be rejected without touching the model.
	public static object? ParseForCommit(object? value, string? formatKind, bool allowsEmpty)
	{
		var text = value?.ToString()?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return allowsEmpty ? string.Empty : BindingOperations.DoNothing;
		}

		if (!string.Equals(formatKind, TimeFormatHelper.TimeHmsFormat, StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}

		var parsed = TimeFormatHelper.ParseValue(text);

		return parsed == text && text.Contains(':') ? BindingOperations.DoNothing : parsed;
	}
}
