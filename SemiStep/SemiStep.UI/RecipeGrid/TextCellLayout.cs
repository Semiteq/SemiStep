using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// The display of a value cell in either grid: a number right-aligned before its unit slot, or left text.
/// </summary>
internal static class TextCellLayout
{
	public const string ValueClass = "cell-value";
	public const string UnitClass = "cell-unit";

	/// <summary>The space between a number and its unit; part of every non-empty unit slot.</summary>
	public const double UnitGap = 4;

	/// <summary>(unit, format kind) to the unit text; a time_hms value shows no unit.</summary>
	public static readonly FuncMultiValueConverter<string?, string> UnitTextConverter =
		new(unitAndFormat =>
		{
			var isTime = unitAndFormat.Count > 1
				&& string.Equals(unitAndFormat[1], TimeFormatHelper.TimeHmsFormat, StringComparison.OrdinalIgnoreCase);

			return isTime || unitAndFormat.Count == 0 ? string.Empty : unitAndFormat[0] ?? string.Empty;
		});

	/// <summary>The editor padding that ends its text where the display does, before the unit slot.</summary>
	public static Thickness EditorPadding(GridStyleLayout layout, bool isNumeric, double unitSlotWidth)
	{
		return new Thickness(
			layout.CellPaddingLeft,
			layout.CellPaddingTop,
			layout.CellPaddingRight + (isNumeric ? unitSlotWidth : 0),
			layout.CellPaddingBottom);
	}

	public static Control Create(
		GridStyleOptions gridStyle,
		bool isNumeric,
		double unitSlotWidth,
		BindingBase valueText,
		BindingBase? unitText)
	{
		var layout = gridStyle.Layout;
		var value = new TextBlock
		{
			VerticalAlignment = VerticalAlignment.Center,
			TextAlignment = isNumeric ? TextAlignment.Right : TextAlignment.Left,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Padding = new Thickness(
				layout.CellPaddingLeft,
				layout.CellPaddingTop,
				isNumeric && unitSlotWidth > 0 ? 0 : layout.CellPaddingRight,
				layout.CellPaddingBottom),
		};
		value.Classes.Add(ValueClass);
		value.Bind(TextBlock.TextProperty, valueText);

		if (!isNumeric || unitSlotWidth <= 0 || unitText is null)
		{
			GridFontApplier.ApplyCellFont(value, gridStyle);
			return value;
		}

		var unit = new TextBlock
		{
			VerticalAlignment = VerticalAlignment.Center,
			Padding = new Thickness(UnitGap, layout.CellPaddingTop, layout.CellPaddingRight, layout.CellPaddingBottom),
		};
		unit.Classes.Add(UnitClass);
		unit.Bind(TextBlock.TextProperty, unitText);
		Grid.SetColumn(unit, 1);

		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(unitSlotWidth + layout.CellPaddingRight, GridUnitType.Pixel),
			},
			Children = { value, unit },
		};
		GridFontApplier.ApplyCellFont(grid, gridStyle);

		return grid;
	}
}
