using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal sealed class PropertyTimeEditingConverter(string formatKind, bool isTextColumn) : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return FormatForDisplay(value, formatKind);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return ParseForCommit(value, formatKind, isTextColumn);
	}

	// Units-less editor text shared with the transposed recyclable text editor, which drives the same
	// rendering through a stateless OneWay MultiBinding instead of a per-cell converter. Invariant, as the
	// model parses it: the editor shows 90.5 as "90.5" in every culture.
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
	public static object? ParseForCommit(object? value, string? formatKind, bool isTextColumn)
	{
		var text = value?.ToString()?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return isTextColumn ? string.Empty : BindingOperations.DoNothing;
		}

		var editorText = isTextColumn ? text : WithDecimalPoint(text);
		if (!string.Equals(formatKind, TimeFormatHelper.TimeHmsFormat, StringComparison.OrdinalIgnoreCase)
			|| HasBothSeparators(editorText))
		{
			return editorText;
		}

		var parsed = TimeFormatHelper.ParseValue(editorText);

		return parsed == editorText && editorText.Contains(':') ? BindingOperations.DoNothing : parsed;
	}

	// A decimal comma becomes a point in every culture, when the result is a number or a time. Any other text
	// passes as typed, so the model parser rejects it and its error quotes what the operator typed.
	private static string WithDecimalPoint(string text)
	{
		if (!text.Contains(',') || text.Contains('.'))
		{
			return text;
		}

		var rewritten = text.Replace(',', '.');
		var isNumber = float.TryParse(rewritten, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
		var isTime = TimeFormatHelper.ParseValue(rewritten) != rewritten;

		return isNumber || isTime ? rewritten : text;
	}

	private static bool HasBothSeparators(string text)
	{
		return text.Contains(',') && text.Contains('.');
	}
}
