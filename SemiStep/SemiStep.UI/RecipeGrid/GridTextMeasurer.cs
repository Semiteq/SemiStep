using System.Globalization;

using Avalonia.Media;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.RecipeGrid;

/// <summary>Measures cell and header text in the configured grid fonts, as the cells render it.</summary>
internal sealed class GridTextMeasurer(GridStyleOptions gridStyle)
{
	// Empty family means "grid default": measure with the chrome font so the width matches the
	// rendered cell, which also falls back to the chrome font.
	private FontFamily MeasureFontFamily =>
		string.IsNullOrWhiteSpace(gridStyle.Fonts.FontFamily)
			? GridFonts.DefaultFamily
			: new FontFamily(gridStyle.Fonts.FontFamily);

	private Typeface CellTypeface =>
		new(
			MeasureFontFamily,
			gridStyle.Fonts.CellItalic ? FontStyle.Italic : FontStyle.Normal,
			(FontWeight)gridStyle.Fonts.CellFontWeight);

	private Typeface HeaderTypeface =>
		new(
			MeasureFontFamily,
			gridStyle.Fonts.HeaderItalic ? FontStyle.Italic : FontStyle.Normal,
			(FontWeight)gridStyle.Fonts.HeaderFontWeight);

	public double MeasureContent(string text)
	{
		return MeasureText(text, CellTypeface, gridStyle.Fonts.CellFontSize);
	}

	public double MeasureHeaderWord(string word)
	{
		return MeasureText(word, HeaderTypeface, gridStyle.Fonts.HeaderFontSize);
	}

	private static double MeasureText(string text, Typeface typeface, double fontSize)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}

		var formattedText = new FormattedText(
			text,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			typeface,
			fontSize,
			Brushes.Black);

		// Cells render with tabular figures (GridFontApplier/ColumnBuilder set tnum); measure with the
		// same feature so digit-column auto-width matches the rendered advances.
		formattedText.SetFontFeatures(GridFonts.TabularFigures);

		return formattedText.WidthIncludingTrailingWhitespace;
	}
}
