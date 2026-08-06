using SemiStep.Core.Configuration;

namespace SemiStep.Tests.Helpers;

/// <summary>
/// Shared test fixture for the grid-style anti-regression guards. <see cref="Distinct"/> returns a
/// <see cref="GridStyleOptions"/> in which every surfaced field holds a distinct, valid, and
/// exactly-representable value, so that a dropped or cross-wired Seed/BuildRecord/mapper line makes
/// exactly one field mismatch. Colors are 53 mutually-distinct UPPERCASE opaque #RRGGBB literals
/// (equality is string equality; <c>ToHex</c> emits X2). Numerics are integers or clean halves so the
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
				Background: "#010101",
				Foreground: "#020202"),
			ChangedCells: new ChangedCellColors(
				Changed: "#030303",
				ChangedSelected: "#040404"),
			DisabledCells: new DepthPalette(
				Depth0: "#050505",
				Depth1: "#060606",
				Depth2: "#070707",
				Depth3: "#080808",
				Depth0Past: "#090909",
				Depth1Past: "#0A0A0A",
				Depth2Past: "#0B0B0B",
				Depth3Past: "#0C0C0C",
				Selected: "#0D0D0D",
				Foreground: "#0E0E0E"),
			ReadOnlyCells: new DepthPalette(
				Depth0: "#0F0F0F",
				Depth1: "#101010",
				Depth2: "#111111",
				Depth3: "#121212",
				Depth0Past: "#131313",
				Depth1Past: "#141414",
				Depth2Past: "#151515",
				Depth3Past: "#161616",
				Selected: "#171717",
				Foreground: "#181818"),
			Chrome: new ChromeColors(
				Info: "#292929",
				Connected: "#2A2A2A",
				Disconnected: "#2B2B2B",
				LocalMode: "#2C2C2C",
				Connecting: "#2D2D2D",
				PanelBackground: "#2E2E2E",
				PanelHeaderBackground: "#2F2F2F",
				SubtleBorder: "#303030",
				Separator: "#313131",
				SecondaryForeground: "#323232",
				GridBorder: "#333333",
				GridBackground: "#343434",
				HeaderForeground: "#353535",
				GridLine: "#191919"),
			StatusBar: new StatusBarStyle(
				Background: "#1A1A1A",
				Foreground: "#1B1B1B",
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
				Background: "#1C1C1C",
				Foreground: "#1D1D1D",
				ErrorColor: "#1E1E1E",
				WarningColor: "#1F1F1F",
				MaxHeight: 150.5),
			Execution: new ExecutionPalette(
				Depth0: "#202020",
				Depth1: "#212121",
				Depth2: "#222222",
				Depth3: "#232323",
				Depth0Past: "#242424",
				Depth1Past: "#252525",
				Depth2Past: "#262626",
				Depth3Past: "#272727",
				CurrentStepMarker: "#282828"),
			Orientation: GridOrientation.ColumnsAsSteps);
	}
}
