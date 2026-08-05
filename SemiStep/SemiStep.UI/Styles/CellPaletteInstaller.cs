using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Styles;

internal static class CellPaletteInstaller
{
	public const string CellReadOnlyDepth0BrushKey = "CellReadOnlyDepth0Brush";
	public const string CellReadOnlyDepth1BrushKey = "CellReadOnlyDepth1Brush";
	public const string CellReadOnlyDepth2BrushKey = "CellReadOnlyDepth2Brush";
	public const string CellReadOnlyDepth3BrushKey = "CellReadOnlyDepth3Brush";
	public const string CellReadOnlyDepth0PastBrushKey = "CellReadOnlyDepth0PastBrush";
	public const string CellReadOnlyDepth1PastBrushKey = "CellReadOnlyDepth1PastBrush";
	public const string CellReadOnlyDepth2PastBrushKey = "CellReadOnlyDepth2PastBrush";
	public const string CellReadOnlyDepth3PastBrushKey = "CellReadOnlyDepth3PastBrush";
	public const string CellReadOnlySelectedBackgroundBrushKey = "CellReadOnlySelectedBackgroundBrush";
	public const string CellReadOnlyForegroundBrushKey = "CellReadOnlyForegroundBrush";
	public const string CellDisabledDepth0BrushKey = "CellDisabledDepth0Brush";
	public const string CellDisabledDepth1BrushKey = "CellDisabledDepth1Brush";
	public const string CellDisabledDepth2BrushKey = "CellDisabledDepth2Brush";
	public const string CellDisabledDepth3BrushKey = "CellDisabledDepth3Brush";
	public const string CellDisabledDepth0PastBrushKey = "CellDisabledDepth0PastBrush";
	public const string CellDisabledDepth1PastBrushKey = "CellDisabledDepth1PastBrush";
	public const string CellDisabledDepth2PastBrushKey = "CellDisabledDepth2PastBrush";
	public const string CellDisabledDepth3PastBrushKey = "CellDisabledDepth3PastBrush";
	public const string CellDisabledSelectedBackgroundBrushKey = "CellDisabledSelectedBackgroundBrush";
	public const string CellDisabledForegroundBrushKey = "CellDisabledForegroundBrush";
	public const string SelectionBackgroundBrushKey = "SelectionBackgroundBrush";
	public const string SelectionForegroundBrushKey = "SelectionForegroundBrush";
	public const string CellChangedBrushKey = "CellChangedBrush";
	public const string CellChangedSelectedBackgroundBrushKey = "CellChangedSelectedBackgroundBrush";
	public const string GridLineBrushKey = "GridLineBrush";
	public const string StatusBarBackgroundBrushKey = "StatusBarBackgroundBrush";
	public const string StatusBarForegroundBrushKey = "StatusBarForegroundBrush";
	public const string StatusBarPaddingKey = "StatusBarPadding";
	public const string StatusBarItemSpacingKey = "StatusBarItemSpacing";
	public const string AppFontFamilyKey = "AppFontFamily";
	public const string StatusBarFontSizeKey = "StatusBarFontSize";
	public const string StatusBarFontWeightKey = "StatusBarFontWeight";
	public const string StatusBarFontStyleKey = "StatusBarFontStyle";
	public const string StatusBarTimerLabelFontSizeKey = "StatusBarTimerLabelFontSize";
	public const string StatusBarTimerLabelFontWeightKey = "StatusBarTimerLabelFontWeight";
	public const string StatusBarTimerLabelFontStyleKey = "StatusBarTimerLabelFontStyle";
	public const string StatusBarTimerValueFontSizeKey = "StatusBarTimerValueFontSize";
	public const string StatusBarTimerValueFontWeightKey = "StatusBarTimerValueFontWeight";
	public const string StatusBarTimerValueFontStyleKey = "StatusBarTimerValueFontStyle";
	public const string ErrorBrushKey = "ErrorBrush";
	public const string WarningBrushKey = "WarningBrush";
	public const string ValidationPanelBackgroundBrushKey = "ValidationPanelBackgroundBrush";
	public const string ValidationPanelForegroundBrushKey = "ValidationPanelForegroundBrush";
	public const string ValidationPanelMaxHeightKey = "ValidationPanelMaxHeight";
	public const string InfoBrushKey = "InfoBrush";
	public const string ConnectedBrushKey = "ConnectedBrush";
	public const string DisconnectedBrushKey = "DisconnectedBrush";
	public const string LocalModeBrushKey = "LocalModeBrush";
	public const string ConnectingBrushKey = "ConnectingBrush";
	public const string PanelBackgroundBrushKey = "PanelBackgroundBrush";
	public const string PanelHeaderBackgroundBrushKey = "PanelHeaderBackgroundBrush";
	public const string SubtleBorderBrushKey = "SubtleBorderBrush";
	public const string SeparatorBrushKey = "SeparatorBrush";
	public const string SecondaryForegroundBrushKey = "SecondaryForegroundBrush";
	public const string GridBorderBrushKey = "GridBorderBrush";
	public const string GridBackgroundBrushKey = "GridBackgroundBrush";
	public const string HeaderForegroundBrushKey = "HeaderForegroundBrush";
	public const string RowHeightKey = "GridRowHeight";

	public static void Install(IResourceDictionary resources, GridStyleOptions gridStyle)
	{
		ArgumentNullException.ThrowIfNull(resources);
		ArgumentNullException.ThrowIfNull(gridStyle);

		resources[CellReadOnlyDepth0BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Depth0);
		resources[CellReadOnlyDepth1BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Depth1);
		resources[CellReadOnlyDepth2BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Depth2);
		resources[CellReadOnlyDepth3BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Depth3);
		resources[CellReadOnlyDepth0PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Depth0Past);
		resources[CellReadOnlyDepth1PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Depth1Past);
		resources[CellReadOnlyDepth2PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Depth2Past);
		resources[CellReadOnlyDepth3PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Depth3Past);
		resources[CellReadOnlySelectedBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Selected);
		resources[CellReadOnlyForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCells.Foreground);
		resources[CellDisabledDepth0BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Depth0);
		resources[CellDisabledDepth1BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Depth1);
		resources[CellDisabledDepth2BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Depth2);
		resources[CellDisabledDepth3BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Depth3);
		resources[CellDisabledDepth0PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Depth0Past);
		resources[CellDisabledDepth1PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Depth1Past);
		resources[CellDisabledDepth2PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Depth2Past);
		resources[CellDisabledDepth3PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Depth3Past);
		resources[CellDisabledSelectedBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Selected);
		resources[CellDisabledForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCells.Foreground);
		resources[SelectionBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.Selection.Background);
		resources[SelectionForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.Selection.Foreground);
		resources[CellChangedBrushKey] = PaletteBrushFactory.From(gridStyle.ChangedCells.Changed);
		resources[CellChangedSelectedBackgroundBrushKey] =
			PaletteBrushFactory.From(gridStyle.ChangedCells.ChangedSelected);
		resources[GridLineBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.GridLine);
		resources[StatusBarBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.StatusBar.Background);
		resources[StatusBarForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.StatusBar.Foreground);
		resources[StatusBarPaddingKey] = new Thickness(gridStyle.StatusBar.Padding);
		resources[StatusBarItemSpacingKey] = gridStyle.StatusBar.ItemSpacing;
		resources[AppFontFamilyKey] = ToFontFamily(gridStyle.Fonts.FontFamily);
		resources[StatusBarFontSizeKey] = (double)gridStyle.StatusBar.FontSize;
		resources[StatusBarFontWeightKey] = (FontWeight)gridStyle.StatusBar.Weight;
		resources[StatusBarFontStyleKey] = ToFontStyle(gridStyle.StatusBar.Italic);
		resources[StatusBarTimerLabelFontSizeKey] = (double)gridStyle.StatusBar.TimerLabelFontSize;
		resources[StatusBarTimerLabelFontWeightKey] = (FontWeight)gridStyle.StatusBar.TimerLabelWeight;
		resources[StatusBarTimerLabelFontStyleKey] = ToFontStyle(gridStyle.StatusBar.TimerLabelItalic);
		resources[StatusBarTimerValueFontSizeKey] = (double)gridStyle.StatusBar.TimerValueFontSize;
		resources[StatusBarTimerValueFontWeightKey] = (FontWeight)gridStyle.StatusBar.TimerValueWeight;
		resources[StatusBarTimerValueFontStyleKey] = ToFontStyle(gridStyle.StatusBar.TimerValueItalic);
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
		resources[LocalModeBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.LocalMode);
		resources[ConnectingBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.Connecting);
		resources[PanelBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.PanelBackground);
		resources[PanelHeaderBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.PanelHeaderBackground);
		resources[SubtleBorderBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.SubtleBorder);
		resources[SeparatorBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.Separator);
		resources[SecondaryForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.SecondaryForeground);
		resources[GridBorderBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.GridBorder);
		resources[GridBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.GridBackground);
		resources[HeaderForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.Chrome.HeaderForeground);
		resources[RowHeightKey] = gridStyle.Layout.RowHeight;
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
