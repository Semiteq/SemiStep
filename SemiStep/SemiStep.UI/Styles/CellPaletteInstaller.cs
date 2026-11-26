using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Styles;

internal static class CellPaletteInstaller
{
	public const string GridLineBrushKey = "GridLineBrush";
	public const string StatusBarBackgroundBrushKey = "StatusBarBackgroundBrush";
	public const string StatusBarForegroundBrushKey = "StatusBarForegroundBrush";
	public const string StatusBarPaddingKey = "StatusBarPadding";
	public const string AppFontFamilyKey = "AppFontFamily";
	public const string StatusBarFontSizeKey = "StatusBarFontSize";
	public const string StatusBarFontWeightKey = "StatusBarFontWeight";
	public const string StatusBarFontStyleKey = "StatusBarFontStyle";
	public const string ErrorBrushKey = "ErrorBrush";
	public const string WarningBrushKey = "WarningBrush";
	public const string ValidationPanelBackgroundBrushKey = "ValidationPanelBackgroundBrush";
	public const string ValidationPanelForegroundBrushKey = "ValidationPanelForegroundBrush";
	public const string ValidationPanelMaxHeightKey = "ValidationPanelMaxHeight";
	public const string InfoBrushKey = "InfoBrush";
	public const string ConnectedBrushKey = "ConnectedBrush";
	public const string DisconnectedBrushKey = "DisconnectedBrush";
	public const string PanelBackgroundBrushKey = "PanelBackgroundBrush";
	public const string PanelHeaderBackgroundBrushKey = "PanelHeaderBackgroundBrush";
	public const string SubtleBorderBrushKey = "SubtleBorderBrush";
	public const string SeparatorBrushKey = "SeparatorBrush";
	public const string SecondaryForegroundBrushKey = "SecondaryForegroundBrush";
	public const string GridBorderBrushKey = "GridBorderBrush";
	public const string GridBackgroundBrushKey = "GridBackgroundBrush";
	public const string HeaderForegroundBrushKey = "HeaderForegroundBrush";
	public const string RowHeightKey = "GridRowHeight";
	public const string DataGridCellCurrentBorderBrushKey = "DataGridCellCurrentBorderBrush";
	public const string DataGridCellFocusBorderBrushKey = "DataGridCellFocusBorderBrush";
	public const string CellFocusFrameKey = "CellFocusFrame";

	private const double FocusFrameThickness = 2;

	public static void Install(IResourceDictionary resources, GridStyleOptions gridStyle)
	{
		ArgumentNullException.ThrowIfNull(resources);
		ArgumentNullException.ThrowIfNull(gridStyle);

		resources[CellPalette.ResourceKey] = CellPalette.Build(gridStyle);
		resources[GridLineBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.GridLine);
		resources[StatusBarBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.StatusBar.Background);
		resources[StatusBarForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.StatusBar.Foreground);
		resources[StatusBarPaddingKey] = new Thickness(0, gridStyle.StatusBar.Padding, 0, gridStyle.StatusBar.Padding);
		resources[AppFontFamilyKey] = ToFontFamily(gridStyle.Fonts.FontFamily);
		resources[StatusBarFontSizeKey] = (double)gridStyle.StatusBar.FontSize;
		resources[StatusBarFontWeightKey] = (FontWeight)gridStyle.StatusBar.Weight;
		resources[StatusBarFontStyleKey] = ToFontStyle(gridStyle.StatusBar.Italic);
		resources[ErrorBrushKey] = PaletteBrushFactory.From(gridStyle.ValidationPanel.ErrorColor);
		resources[WarningBrushKey] = PaletteBrushFactory.From(gridStyle.ValidationPanel.WarningColor);
		resources[ValidationPanelBackgroundBrushKey] =
			PaletteBrushFactory.From(gridStyle.ValidationPanel.Background);
		resources[ValidationPanelForegroundBrushKey] =
			PaletteBrushFactory.From(gridStyle.ValidationPanel.Foreground);
		resources[ValidationPanelMaxHeightKey] = gridStyle.ValidationPanel.MaxHeight;
		resources[InfoBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.Info);
		resources[ConnectedBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.Connected);
		resources[DisconnectedBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.Disconnected);
		resources[PanelBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.PanelBackground);
		resources[PanelHeaderBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.PanelHeaderBackground);
		resources[SubtleBorderBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.SubtleBorder);
		resources[SeparatorBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.Separator);
		resources[SecondaryForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.SecondaryForeground);
		resources[GridBorderBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.GridBorder);
		resources[GridBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.GridBackground);
		resources[HeaderForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.HeaderForeground);
		resources[RowHeightKey] = gridStyle.Layout.RowHeight;
		InstallFocusFrame(resources, gridStyle.Selection.Accent);
	}

	// The focused cell's frame in the selection accent: Semi's current-cell rectangles read the two DataGrid
	// keys; the transposed cell draws an inset shadow, which moves no layout.
	private static void InstallFocusFrame(IResourceDictionary resources, StyleColor accent)
	{
		resources[DataGridCellCurrentBorderBrushKey] = PaletteBrushFactory.From(accent);
		resources[DataGridCellFocusBorderBrushKey] = PaletteBrushFactory.From(accent);
		resources[CellFocusFrameKey] = new BoxShadows(new BoxShadow
		{
			IsInset = true,
			Spread = FocusFrameThickness,
			Color = accent.ToMediaColor(),
		});
	}

	// An empty family means "theme default": FontFamily.Default keeps the application's default
	// typeface instead of overriding it with a specific font.
	private static FontFamily ToFontFamily(string family)
	{
		return string.IsNullOrWhiteSpace(family) ? FontFamily.Default : new FontFamily(family);
	}

	private static FontStyle ToFontStyle(bool italic)
	{
		return italic ? FontStyle.Italic : FontStyle.Normal;
	}
}
