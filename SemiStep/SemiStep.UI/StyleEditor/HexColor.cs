using Avalonia.Media;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Converts between the config's <c>#RRGGBB</c> / <c>#AARRGGBB</c> hex strings and Avalonia
/// <see cref="Color"/>. <see cref="ToHex"/> is manual rather than <see cref="Color.ToString"/>
/// because the latter always emits <c>#AARRGGBB</c>, which would inject an <c>#FF</c> alpha
/// prefix onto opaque colors and break the config's <c>#RRGGBB</c> style on round-trip.
/// </summary>
internal static class HexColor
{
	public static Color Parse(string hex)
	{
		return Color.Parse(hex);
	}

	public static string ToHex(Color color)
	{
		if (color.A == byte.MaxValue)
		{
			return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
		}

		return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
	}
}
