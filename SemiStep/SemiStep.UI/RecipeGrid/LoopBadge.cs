using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace SemiStep.UI.RecipeGrid;

/// <summary>The "(pass/count)" pill of a running loop in the action cell of its "Цикл начало" row.</summary>
internal static class LoopBadge
{
	public const string PillClass = "loop-badge";

	private const double TextFontSize = 12;
	private const FontWeight TextFontWeight = FontWeight.SemiBold;
	private const double HorizontalPadding = 8;
	private const double BorderWidth = 1;
	private const double PillHeight = 20;
	private const double PillCornerRadius = 10;

	// Two-digit pass and count; a loop of 100 passes or more outgrows the width the columns reserve.
	private const string WidestPassText = "(99/99)";

	public static Border Create(string loopPassTextPath)
	{
		var text = new TextBlock
		{
			VerticalAlignment = VerticalAlignment.Center,
			FontFamily = GridFonts.DefaultFamily,
			FontSize = TextFontSize,
			FontWeight = TextFontWeight,
		};
		text.Bind(TextBlock.TextProperty, new Binding(loopPassTextPath) { Mode = BindingMode.OneWay });

		var pill = new Border
		{
			VerticalAlignment = VerticalAlignment.Center,
			Height = PillHeight,
			CornerRadius = new CornerRadius(PillCornerRadius),
			BorderThickness = new Thickness(BorderWidth),
			Padding = new Thickness(HorizontalPadding, 0),
			IsHitTestVisible = false,
			Child = text,
		};
		pill.Classes.Add(PillClass);
		pill.Bind(Visual.IsVisibleProperty, new Binding(loopPassTextPath)
		{
			Mode = BindingMode.OneWay,
			Converter = StringConverters.IsNotNullOrEmpty,
		});

		return pill;
	}

	/// <summary>The width of the pill showing "(99/99)": its text, padding and border.</summary>
	public static double WidestWidth()
	{
		var text = new FormattedText(
			WidestPassText,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			new Typeface(GridFonts.DefaultFamily, FontStyle.Normal, TextFontWeight),
			TextFontSize,
			Brushes.Black);

		return Math.Ceiling(text.WidthIncludingTrailingWhitespace + (2 * (HorizontalPadding + BorderWidth)));
	}
}
