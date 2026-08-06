using Avalonia.Media;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Styles;

internal static class PaletteBrushFactory
{
	public static SolidColorBrush From(StyleColor color)
	{
		return new SolidColorBrush(color.ToMediaColor());
	}
}
