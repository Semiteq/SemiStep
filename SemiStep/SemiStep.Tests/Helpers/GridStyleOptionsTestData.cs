using SemiStep.Core.Configuration;

namespace SemiStep.Tests.Helpers;

/// <summary>
/// Shared test fixture for the grid-style anti-regression guards. <see cref="Distinct"/> returns a
/// <see cref="GridStyleOptions"/> in which every surfaced field holds a distinct, valid, and
/// exactly-representable value, so that a dropped or cross-wired Seed/BuildRecord/mapper line makes
/// exactly one field mismatch. Colors are 53 mutually-distinct UPPERCASE opaque #RRGGBB literals
/// (equality is channel equality; <c>StyleColor.ToString</c> emits X2). Numerics are integers or clean halves so the
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
				Background: StyleColor.Parse("#010101"),
				Foreground: StyleColor.Parse("#020202")),
			ChangedCells: new ChangedCellColors(
				Changed: StyleColor.Parse("#030303"),
				ChangedSelected: StyleColor.Parse("#040404")),
			DisabledCells: new DepthPalette(
				Depth0: StyleColor.Parse("#050505"),
				Depth1: StyleColor.Parse("#060606"),
				Depth2: StyleColor.Parse("#070707"),
				Depth3: StyleColor.Parse("#080808"),
				Depth0Past: StyleColor.Parse("#090909"),
				Depth1Past: StyleColor.Parse("#0A0A0A"),
				Depth2Past: StyleColor.Parse("#0B0B0B"),
				Depth3Past: StyleColor.Parse("#0C0C0C"),
				Selected: StyleColor.Parse("#0D0D0D"),
				Foreground: StyleColor.Parse("#0E0E0E")),
			ReadOnlyCells: new DepthPalette(
				Depth0: StyleColor.Parse("#0F0F0F"),
				Depth1: StyleColor.Parse("#101010"),
				Depth2: StyleColor.Parse("#111111"),
				Depth3: StyleColor.Parse("#121212"),
				Depth0Past: StyleColor.Parse("#131313"),
				Depth1Past: StyleColor.Parse("#141414"),
				Depth2Past: StyleColor.Parse("#151515"),
				Depth3Past: StyleColor.Parse("#161616"),
				Selected: StyleColor.Parse("#171717"),
				Foreground: StyleColor.Parse("#181818")),
			Chrome: new ChromeColors(
				Info: StyleColor.Parse("#292929"),
				Connected: StyleColor.Parse("#2A2A2A"),
				Disconnected: StyleColor.Parse("#2B2B2B"),
				LocalMode: StyleColor.Parse("#2C2C2C"),
				Connecting: StyleColor.Parse("#2D2D2D"),
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
				ItemSpacing: 10.5,
				FontSize: 17,
				Weight: 500,
				Italic: true,
				TimerLabelFontSize: 19,
				TimerLabelWeight: 600,
				TimerLabelItalic: true,
				TimerValueFontSize: 23,
				TimerValueWeight: 700,
				TimerValueItalic: true),
			ValidationPanel: new ValidationPanelStyle(
				Background: StyleColor.Parse("#1C1C1C"),
				Foreground: StyleColor.Parse("#1D1D1D"),
				ErrorColor: StyleColor.Parse("#1E1E1E"),
				WarningColor: StyleColor.Parse("#1F1F1F"),
				MaxHeight: 150.5),
			Execution: new ExecutionPalette(
				Depth0: StyleColor.Parse("#202020"),
				Depth1: StyleColor.Parse("#212121"),
				Depth2: StyleColor.Parse("#222222"),
				Depth3: StyleColor.Parse("#232323"),
				Depth0Past: StyleColor.Parse("#242424"),
				Depth1Past: StyleColor.Parse("#252525"),
				Depth2Past: StyleColor.Parse("#262626"),
				Depth3Past: StyleColor.Parse("#272727"),
				CurrentStepMarker: StyleColor.Parse("#282828")),
			Orientation: GridOrientation.ColumnsAsSteps);
	}
}
