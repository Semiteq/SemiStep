using Avalonia.Media;

namespace SemiStep.UI.Styles;

internal static class PaletteBrushFactory
{
	public static SolidColorBrush From(string hex)
	{
		return new SolidColorBrush(Color.Parse(hex));
	}
}
