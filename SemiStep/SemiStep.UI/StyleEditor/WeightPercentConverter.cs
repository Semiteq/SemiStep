using System.Globalization;

using Avalonia.Data.Converters;

namespace SemiStep.UI.StyleEditor;

/// <summary>Shows a 0..1 weight as a 0..100 percentage and stores the percentage back as 0..1.</summary>
public sealed class WeightPercentConverter : IValueConverter
{
	private const decimal Percent = 100;

	public static WeightPercentConverter Instance { get; } = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is decimal weight ? weight * Percent : null;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is decimal percent ? percent / Percent : null;
	}
}
