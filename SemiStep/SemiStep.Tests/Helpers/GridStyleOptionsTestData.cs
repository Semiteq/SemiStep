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
/// proves BuildRecord preserves the unsurfaced field through <c>with</c>.
/// </summary>
public static class GridStyleOptionsTestData
{
	public static GridStyleOptions Distinct()
	{
		return new GridStyleOptions(
			FontFamily: "Distinct Test Font",
			HeaderFontSize: 13,
			HeaderFontWeight: 300,
			HeaderItalic: true,
			CellFontSize: 11,
			CellFontWeight: 800,
			CellItalic: true,
			CellPaddingLeft: 6.5,
			CellPaddingTop: 4.5,
			CellPaddingRight: 7.5,
			CellPaddingBottom: 3.5,
			RowHeight: 28.5,
			SelectionBackgroundColor: "#010101",
			SelectionForegroundColor: "#020202",
			CellChangedColor: "#030303",
			CellChangedSelectedColor: "#040404",
			DisabledCellDepth0Color: "#050505",
			DisabledCellDepth1Color: "#060606",
			DisabledCellDepth2Color: "#070707",
			DisabledCellDepth3Color: "#080808",
			DisabledCellDepth0PastColor: "#090909",
			DisabledCellDepth1PastColor: "#0A0A0A",
			DisabledCellDepth2PastColor: "#0B0B0B",
			DisabledCellDepth3PastColor: "#0C0C0C",
			DisabledCellSelectedColor: "#0D0D0D",
			DisabledCellForegroundColor: "#0E0E0E",
			ReadOnlyCellDepth0Color: "#0F0F0F",
			ReadOnlyCellDepth1Color: "#101010",
			ReadOnlyCellDepth2Color: "#111111",
			ReadOnlyCellDepth3Color: "#121212",
			ReadOnlyCellDepth0PastColor: "#131313",
			ReadOnlyCellDepth1PastColor: "#141414",
			ReadOnlyCellDepth2PastColor: "#151515",
			ReadOnlyCellDepth3PastColor: "#161616",
			ReadOnlyCellSelectedColor: "#171717",
			ReadOnlyCellForegroundColor: "#181818",
			GridLineColor: "#191919",
			StatusBarBackgroundColor: "#1A1A1A",
			StatusBarForegroundColor: "#1B1B1B",
			StatusBarPadding: 5.5,
			StatusBarItemSpacing: 10.5,
			StatusBarFontSize: 17,
			StatusBarFontWeight: 500,
			StatusBarItalic: true,
			StatusBarTimerLabelFontSize: 19,
			StatusBarTimerLabelFontWeight: 600,
			StatusBarTimerLabelItalic: true,
			StatusBarTimerValueFontSize: 23,
			StatusBarTimerValueFontWeight: 700,
			StatusBarTimerValueItalic: true,
			ValidationPanelBackgroundColor: "#1C1C1C",
			ValidationPanelForegroundColor: "#1D1D1D",
			ValidationPanelErrorColor: "#1E1E1E",
			ValidationPanelWarningColor: "#1F1F1F",
			ValidationPanelMaxHeight: 150.5,
			ExecutionDepth0Color: "#202020",
			ExecutionDepth1Color: "#212121",
			ExecutionDepth2Color: "#222222",
			ExecutionDepth3Color: "#232323",
			ExecutionDepth0PastColor: "#242424",
			ExecutionDepth1PastColor: "#252525",
			ExecutionDepth2PastColor: "#262626",
			ExecutionDepth3PastColor: "#272727",
			ExecutionCurrentStepMarkerColor: "#282828",
			InfoColor: "#292929",
			ConnectedColor: "#2A2A2A",
			DisconnectedColor: "#2B2B2B",
			LocalModeColor: "#2C2C2C",
			ConnectingColor: "#2D2D2D",
			PanelBackgroundColor: "#2E2E2E",
			PanelHeaderBackgroundColor: "#2F2F2F",
			SubtleBorderColor: "#303030",
			SeparatorColor: "#313131",
			SecondaryForegroundColor: "#323232",
			GridBorderColor: "#333333",
			GridBackgroundColor: "#343434",
			HeaderForegroundColor: "#353535",
			Orientation: GridOrientation.ColumnsAsSteps);
	}
}
