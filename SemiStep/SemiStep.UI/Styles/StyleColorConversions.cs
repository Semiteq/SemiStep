using Avalonia.Media;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Styles;

/// <summary>
/// Channel-wise bridge between the Avalonia-free <see cref="StyleColor"/> and Avalonia's
/// <see cref="Color"/>. Both carry the same A/R/G/B bytes, so the conversion copies channels
/// directly — no hex parse or format on the round-trip.
/// </summary>
internal static class StyleColorConversions
{
	public static Color ToMediaColor(this StyleColor color)
	{
		return Color.FromArgb(color.A, color.R, color.G, color.B);
	}

	public static StyleColor ToStyleColor(this Color color)
	{
		return new StyleColor(color.A, color.R, color.G, color.B);
	}
}
