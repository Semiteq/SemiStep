using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.RecipeGrid;

// Grid fonts are assigned in code (not via the resource cascade). This applies the configured
// family/weight/italic/size onto a control built by the cell and column factories so they all
// honour the same empty-family rule. See Docs/architecture/grid-style-configuration.md.
internal static class GridFontApplier
{
	public static void ApplyHeaderFont(Control control, GridStyleOptions gridStyle)
	{
		Apply(control, gridStyle.FontFamily, gridStyle.HeaderFontSize, gridStyle.HeaderFontWeight, gridStyle.HeaderItalic);
	}

	public static void ApplyCellFont(Control control, GridStyleOptions gridStyle)
	{
		Apply(control, gridStyle.FontFamily, gridStyle.CellFontSize, gridStyle.CellFontWeight, gridStyle.CellItalic);
	}

	private static void Apply(Control control, string fontFamily, int fontSize, int fontWeight, bool italic)
	{
		control.SetValue(TextElement.FontSizeProperty, (double)fontSize);
		control.SetValue(TextElement.FontWeightProperty, (FontWeight)fontWeight);
		control.SetValue(TextElement.FontStyleProperty, italic ? FontStyle.Italic : FontStyle.Normal);

		// Empty family means "grid default": fall back to the chrome font (system Segoe).
		control.SetValue(
			TextElement.FontFamilyProperty,
			string.IsNullOrWhiteSpace(fontFamily) ? GridFonts.DefaultFamily : new FontFamily(fontFamily));

		// Tabular figures so digit columns align under the proportional chrome font.
		control.SetValue(TextElement.FontFeaturesProperty, GridFonts.TabularFigures);
	}
}
