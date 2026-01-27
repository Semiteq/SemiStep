using Avalonia;
using Avalonia.Media;

using Serilog;

namespace UI.Helpers;

public static class StyleHelper
{
	private static readonly Color _errorColor = Color.Parse("#FF00FF");

	public static IBrush ToBrush(string hexColor)
	{
		try
		{
			return new SolidColorBrush(Color.Parse(hexColor));
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Failed to parse color '{HexColor}', using error color", hexColor);
			return new SolidColorBrush(_errorColor);
		}
	}

	public static Thickness ToThickness(double left, double top, double right, double bottom)
	{
		return new Thickness(left, top, right, bottom);
	}

	public static Thickness ToUniformThickness(double value)
	{
		return new Thickness(value);
	}
}
