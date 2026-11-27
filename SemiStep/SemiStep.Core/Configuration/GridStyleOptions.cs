namespace SemiStep.Core.Configuration;

public sealed record GridStyleOptions(
	GridStyleFonts Fonts,
	GridStyleLayout Layout,
	SelectionColors Selection,
	ChangedCellColors ChangedCells,
	StyleColor ReadOnlyCell,
	StyleColor DisabledCell,
	ExecutionPalette Execution,
	LoopPalette Loop,
	StatusBarStyle StatusBar,
	ValidationPanelStyle ValidationPanel,
	ChromeColors Chrome,
	GridOrientation Orientation)
{
	// The approved palette; a site file that omits an optional key renders with these values.
	public static GridStyleOptions Default { get; } = new(
		Fonts: new GridStyleFonts(
			FontFamily: "",
			HeaderFontSize: 12,
			HeaderFontWeight: 600,
			HeaderItalic: false,
			CellFontSize: 12,
			CellFontWeight: 400,
			CellItalic: false),
		Layout: new GridStyleLayout(
			CellPaddingLeft: 6,
			CellPaddingTop: 4,
			CellPaddingRight: 6,
			CellPaddingBottom: 4,
			RowHeight: 28),
		Selection: new SelectionColors(
			Accent: StyleColor.Parse("#3574F0"),
			Weight: 0.25),
		ChangedCells: new ChangedCellColors(
			Changed: StyleColor.Parse("#FFCC80")),
		ReadOnlyCell: StyleColor.Parse("#D8D8D8"),
		DisabledCell: StyleColor.Parse("#E0E0E0"),
		Execution: new ExecutionPalette(
			Running: StyleColor.Parse("#208A3C"),
			RunningWeight: 0.30,
			PastWeight: 0.12),
		Loop: new LoopPalette(
			Color: StyleColor.Parse("#7D4DC8"),
			Depth1Weight: 0.08,
			Depth2Weight: 0.16,
			Depth3Weight: 0.26),
		StatusBar: new StatusBarStyle(
			Background: StyleColor.Parse("#F7F8FA"),
			Foreground: StyleColor.Parse("#000000"),
			Padding: 5,
			FontSize: 12,
			Weight: 400,
			Italic: false),
		ValidationPanel: new ValidationPanelStyle(
			Background: StyleColor.Parse("#FFFFFF"),
			Foreground: StyleColor.Parse("#000000"),
			ErrorColor: StyleColor.Parse("#D32F2F"),
			WarningColor: StyleColor.Parse("#F57C00"),
			MaxHeight: 100),
		Chrome: new ChromeColors(
			Info: StyleColor.Parse("#1976D2"),
			Connected: StyleColor.Parse("#208A3C"),
			Disconnected: StyleColor.Parse("#DB3B4B"),
			PanelBackground: StyleColor.Parse("#F8F8F8"),
			PanelHeaderBackground: StyleColor.Parse("#EEEEEE"),
			SubtleBorder: StyleColor.Parse("#D0D0D0"),
			Separator: StyleColor.Parse("#C0C0C0"),
			SecondaryForeground: StyleColor.Parse("#6C707E"),
			GridBorder: StyleColor.Parse("#808080"),
			GridBackground: StyleColor.Parse("#FFFFFF"),
			HeaderForeground: StyleColor.Parse("#6C707E"),
			GridLine: StyleColor.Parse("#B8B8B8")),
		Orientation: GridOrientation.RowsAsSteps);
}
