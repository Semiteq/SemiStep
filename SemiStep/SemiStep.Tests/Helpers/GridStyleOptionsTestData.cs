using SemiStep.Core.Configuration;

namespace SemiStep.Tests.Helpers;

/// <summary>
/// Shared test fixture for the grid-style anti-regression guards. <see cref="Distinct"/> returns a
/// <see cref="GridStyleOptions"/> in which every surfaced field holds a distinct, valid, and
/// exactly-representable value, so that a dropped or cross-wired Seed/BuildRecord/mapper line makes
/// exactly one field mismatch. Colors are mutually-distinct UPPERCASE opaque #RRGGBB literals
/// (equality is channel equality; <c>StyleColor.ToString</c> emits X2). Numerics are integers or exact binary fractions so the
/// int↔decimal and double↔decimal conversions are exact. All five italics are <c>true</c> — the one
/// kind whose type default (<c>false</c>) can equal a fixture value — so a dropped italic Seed line
/// surfaces as false ≠ true. <see cref="GridStyleOptions.Orientation"/> is the non-default value, which
/// guards that BuildRecord carries the unsurfaced field through positional construction from
/// <c>_source.Orientation</c>.
/// </summary>
public static class GridStyleOptionsTestData
{
	public static GridStyleOptions Distinct()
	{
		return new GridStyleOptions(
			Fonts: new GridStyleFonts(
				FontFamily: "Distinct Test Font",
				HeaderFontSize: 13,
				HeaderFontWeight: 300,
				HeaderItalic: true,
				CellFontSize: 11,
				CellFontWeight: 800,
				CellItalic: true),
			Layout: new GridStyleLayout(
				CellPaddingLeft: 6.5,
				CellPaddingTop: 4.5,
				CellPaddingRight: 7.5,
				CellPaddingBottom: 3.5,
				RowHeight: 28.5),
			Selection: new SelectionColors(
				Accent: StyleColor.Parse("#010101"),
				Weight: 0.5),
			ChangedCells: new ChangedCellColors(
				Changed: StyleColor.Parse("#030303")),
			DisabledCell: StyleColor.Parse("#050505"),
			ReadOnlyCell: StyleColor.Parse("#0F0F0F"),
			Chrome: new ChromeColors(
				Info: StyleColor.Parse("#292929"),
				Connected: StyleColor.Parse("#2A2A2A"),
				Disconnected: StyleColor.Parse("#2B2B2B"),
				PanelBackground: StyleColor.Parse("#2E2E2E"),
				PanelHeaderBackground: StyleColor.Parse("#2F2F2F"),
				SubtleBorder: StyleColor.Parse("#303030"),
				Separator: StyleColor.Parse("#313131"),
				SecondaryForeground: StyleColor.Parse("#323232"),
				GridBorder: StyleColor.Parse("#333333"),
				GridBackground: StyleColor.Parse("#343434"),
				HeaderForeground: StyleColor.Parse("#353535"),
				GridLine: StyleColor.Parse("#191919")),
			StatusBar: new StatusBarStyle(
				Background: StyleColor.Parse("#1A1A1A"),
				Foreground: StyleColor.Parse("#1B1B1B"),
				Padding: 5.5,
				FontSize: 17,
				Weight: 500,
				Italic: true),
			ValidationPanel: new ValidationPanelStyle(
				Background: StyleColor.Parse("#1C1C1C"),
				Foreground: StyleColor.Parse("#1D1D1D"),
				ErrorColor: StyleColor.Parse("#1E1E1E"),
				WarningColor: StyleColor.Parse("#1F1F1F"),
				MaxHeight: 150.5),
			Execution: new ExecutionPalette(
				Running: StyleColor.Parse("#202020"),
				RunningWeight: 0.75,
				PastWeight: 0.25),
			Loop: new LoopPalette(
				Color: StyleColor.Parse("#212121"),
				Depth1Weight: 0.125,
				Depth2Weight: 0.375,
				Depth3Weight: 0.625),
			Orientation: GridOrientation.ColumnsAsSteps);
	}
}
