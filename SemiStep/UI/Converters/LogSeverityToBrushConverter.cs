using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

using UI.Models;

namespace UI.Converters;

public sealed class LogSeverityToBrushConverter : IValueConverter
{
	public static readonly LogSeverityToBrushConverter Instance = new();

	private static readonly IBrush _errorBrush = new SolidColorBrush(Color.Parse("#D32F2F"));
	private static readonly IBrush _warningBrush = new SolidColorBrush(Color.Parse("#F57C00"));
	private static readonly IBrush _infoBrush = new SolidColorBrush(Color.Parse("#1976D2"));

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is LogSeverity severity
			? severity switch
			{
				LogSeverity.Error => _errorBrush,
				LogSeverity.Warning => _warningBrush,
				_ => _infoBrush,
			}
			: _infoBrush;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
