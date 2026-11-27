using System.Globalization;

using Avalonia.Data.Converters;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Text of a style editor spinner: shows the value with a point and reads "," or "." as the decimal separator
/// in every culture, as the recipe grid's number cells do.
/// </summary>
public sealed class SpinnerTextConverter : IValueConverter
{
	private const NumberStyles TypedNumber =
		NumberStyles.AllowLeadingWhite
		| NumberStyles.AllowTrailingWhite
		| NumberStyles.AllowLeadingSign
		| NumberStyles.AllowDecimalPoint;

	private readonly string _format;
	private readonly bool _isWhole;

	private SpinnerTextConverter(string format, bool isWhole)
	{
		_format = format;
		_isWhole = isWhole;
	}

	public static SpinnerTextConverter Whole { get; } = new("0", isWhole: true);

	public static SpinnerTextConverter Percent { get; } = new("0.#", isWhole: false);

	// NumericUpDown calls Convert for text to value and ConvertBack for value to text. A text the spinner cannot
	// read throws, so the spinner keeps its value and shows it again.
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var text = value as string ?? "";
		if (text.Contains(',') && text.Contains('.'))
		{
			throw new FormatException($"'{text}' holds both decimal separators.");
		}

		var number = decimal.Parse(text.Replace(',', '.'), TypedNumber, CultureInfo.InvariantCulture);
		return _isWhole ? Math.Round(number, MidpointRounding.AwayFromZero) : number;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is decimal number ? number.ToString(_format, CultureInfo.InvariantCulture) : "";
	}
}
