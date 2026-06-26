using Avalonia;
using Avalonia.Controls;

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

	public static void Install(IResourceDictionary resources, GridStyleOptions gridStyle)
	{
		ArgumentNullException.ThrowIfNull(resources);
		ArgumentNullException.ThrowIfNull(gridStyle);

		resources[CellReadOnlyDepth0BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth0Color);
		resources[CellReadOnlyDepth1BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth1Color);
		resources[CellReadOnlyDepth2BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth2Color);
		resources[CellReadOnlyDepth3BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth3Color);
		resources[CellReadOnlyDepth0PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth0PastColor);
		resources[CellReadOnlyDepth1PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth1PastColor);
		resources[CellReadOnlyDepth2PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth2PastColor);
		resources[CellReadOnlyDepth3PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth3PastColor);
		resources[CellReadOnlySelectedBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellSelectedColor);
		resources[CellReadOnlyForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellForegroundColor);
		resources[CellDisabledDepth0BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth0Color);
		resources[CellDisabledDepth1BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth1Color);
		resources[CellDisabledDepth2BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth2Color);
		resources[CellDisabledDepth3BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth3Color);
		resources[CellDisabledDepth0PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth0PastColor);
		resources[CellDisabledDepth1PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth1PastColor);
		resources[CellDisabledDepth2PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth2PastColor);
		resources[CellDisabledDepth3PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth3PastColor);
		resources[CellDisabledSelectedBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellSelectedColor);
		resources[CellDisabledForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellForegroundColor);
		resources[SelectionBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.SelectionBackgroundColor);
		resources[SelectionForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.SelectionForegroundColor);
		resources[CellChangedBrushKey] = PaletteBrushFactory.From(gridStyle.CellChangedColor);
		resources[CellChangedSelectedBackgroundBrushKey] =
			PaletteBrushFactory.From(gridStyle.CellChangedSelectedColor);
		resources[GridLineBrushKey] = PaletteBrushFactory.From(gridStyle.GridLineColor);
		resources[StatusBarBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.StatusBarBackgroundColor);
		resources[StatusBarForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.StatusBarForegroundColor);
		resources[StatusBarPaddingKey] = new Thickness(gridStyle.StatusBarPadding);
		resources[StatusBarItemSpacingKey] = gridStyle.StatusBarItemSpacing;
		resources[ErrorBrushKey] = PaletteBrushFactory.From(gridStyle.ValidationPanelErrorColor);
		resources[WarningBrushKey] = PaletteBrushFactory.From(gridStyle.ValidationPanelWarningColor);
		resources[ValidationPanelBackgroundBrushKey] =
			PaletteBrushFactory.From(gridStyle.ValidationPanelBackgroundColor);
		resources[ValidationPanelForegroundBrushKey] =
			PaletteBrushFactory.From(gridStyle.ValidationPanelForegroundColor);
		resources[ValidationPanelMaxHeightKey] = gridStyle.ValidationPanelMaxHeight;
		resources[InfoBrushKey] = PaletteBrushFactory.From(gridStyle.InfoColor);
		resources[ConnectedBrushKey] = PaletteBrushFactory.From(gridStyle.ConnectedColor);
		resources[DisconnectedBrushKey] = PaletteBrushFactory.From(gridStyle.DisconnectedColor);
		resources[PanelBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.PanelBackgroundColor);
		resources[PanelHeaderBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.PanelHeaderBackgroundColor);
		resources[SubtleBorderBrushKey] = PaletteBrushFactory.From(gridStyle.SubtleBorderColor);
		resources[SeparatorBrushKey] = PaletteBrushFactory.From(gridStyle.SeparatorColor);
		resources[SecondaryForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.SecondaryForegroundColor);
		resources[GridBorderBrushKey] = PaletteBrushFactory.From(gridStyle.GridBorderColor);
		resources[GridBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.GridBackgroundColor);
		resources[HeaderForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.HeaderForegroundColor);
		resources[RowHeightKey] = gridStyle.RowHeight;
	}
}
