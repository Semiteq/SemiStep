using Avalonia.Media;

namespace SemiStep.UI.RecipeGrid;

internal static class GridFonts
{
	// The recipe grid defaults to the chrome font (system Segoe), matching the rest of the app.
	// A non-empty configured family from grid style options overrides this default.
	public static readonly FontFamily DefaultFamily =
		new("Segoe UI Variable Text, Segoe UI");

	// Tabular figures (OpenType tnum) keep digit columns vertically aligned without a monospaced font.
	public static readonly FontFeatureCollection TabularFigures =
		new() { new FontFeature { Tag = "tnum", Value = 1 } };
}
