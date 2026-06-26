using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Mapping;

internal static class GridStyleMapper
{
	public static GridStyleOptions Map(GridStyleOptionsDto? dto)
	{
		if (dto is null)
		{
			return GridStyleOptions.Default;
		}

		var defaults = GridStyleOptions.Default;
		// Presence guaranteed by GridStyleValidator, which runs before this mapper; hence the `!` chain.
		var readOnlyCells = dto.Colors!.Cells!.ReadOnly!;
		var disabledCells = dto.Colors!.Cells!.Disabled!;
		var executionCells = dto.Colors!.Cells!.Execution!;

		return new GridStyleOptions(
			HeaderFontSize: dto.Fonts?.HeaderSize ?? defaults.HeaderFontSize,
			CellFontSize: dto.Fonts?.CellSize ?? defaults.CellFontSize,
			CellPaddingLeft: dto.Layout?.CellPaddingLeft ?? defaults.CellPaddingLeft,
			CellPaddingTop: dto.Layout?.CellPaddingTop ?? defaults.CellPaddingTop,
			CellPaddingRight: dto.Layout?.CellPaddingRight ?? defaults.CellPaddingRight,
			CellPaddingBottom: dto.Layout?.CellPaddingBottom ?? defaults.CellPaddingBottom,
			RowHeight: dto.Layout?.RowHeight ?? defaults.RowHeight,
			SelectionBackgroundColor: dto.Colors?.Selection?.Background ?? defaults.SelectionBackgroundColor,
			SelectionForegroundColor: dto.Colors?.Selection?.Foreground ?? defaults.SelectionForegroundColor,
			CellChangedColor: dto.Colors?.Cells?.Changed ?? defaults.CellChangedColor,
			CellChangedSelectedColor: dto.Colors?.Cells?.ChangedSelected ?? defaults.CellChangedSelectedColor,
			DisabledCellDepth0Color: disabledCells.Depth0!,
			DisabledCellDepth1Color: disabledCells.Depth1!,
			DisabledCellDepth2Color: disabledCells.Depth2!,
			DisabledCellDepth3Color: disabledCells.Depth3!,
			DisabledCellDepth0PastColor: disabledCells.Depth0Past!,
			DisabledCellDepth1PastColor: disabledCells.Depth1Past!,
			DisabledCellDepth2PastColor: disabledCells.Depth2Past!,
			DisabledCellDepth3PastColor: disabledCells.Depth3Past!,
			DisabledCellSelectedColor: disabledCells.Selected!,
			DisabledCellForegroundColor: disabledCells.Foreground!,
			ReadOnlyCellDepth0Color: readOnlyCells.Depth0!,
			ReadOnlyCellDepth1Color: readOnlyCells.Depth1!,
			ReadOnlyCellDepth2Color: readOnlyCells.Depth2!,
			ReadOnlyCellDepth3Color: readOnlyCells.Depth3!,
			ReadOnlyCellDepth0PastColor: readOnlyCells.Depth0Past!,
			ReadOnlyCellDepth1PastColor: readOnlyCells.Depth1Past!,
			ReadOnlyCellDepth2PastColor: readOnlyCells.Depth2Past!,
			ReadOnlyCellDepth3PastColor: readOnlyCells.Depth3Past!,
			ReadOnlyCellSelectedColor: readOnlyCells.Selected!,
			ReadOnlyCellForegroundColor: readOnlyCells.Foreground!,
			GridLineColor: dto.Colors?.GridLine ?? defaults.GridLineColor,
			StatusBarBackgroundColor: dto.StatusBar?.Background ?? defaults.StatusBarBackgroundColor,
			StatusBarForegroundColor: dto.StatusBar?.Foreground ?? defaults.StatusBarForegroundColor,
			StatusBarPadding: dto.StatusBar?.Padding ?? defaults.StatusBarPadding,
			StatusBarItemSpacing: dto.StatusBar?.ItemSpacing ?? defaults.StatusBarItemSpacing,
			ValidationPanelBackgroundColor: dto.ValidationPanel?.Background ?? defaults.ValidationPanelBackgroundColor,
			ValidationPanelForegroundColor: dto.ValidationPanel?.Foreground ?? defaults.ValidationPanelForegroundColor,
			ValidationPanelErrorColor: dto.ValidationPanel?.ErrorColor ?? defaults.ValidationPanelErrorColor,
			ValidationPanelWarningColor: dto.ValidationPanel?.WarningColor ?? defaults.ValidationPanelWarningColor,
			ValidationPanelMaxHeight: dto.ValidationPanel?.MaxHeight ?? defaults.ValidationPanelMaxHeight,
			ExecutionDepth0Color: executionCells.Depth0!,
			ExecutionDepth1Color: executionCells.Depth1!,
			ExecutionDepth2Color: executionCells.Depth2!,
			ExecutionDepth3Color: executionCells.Depth3!,
			ExecutionDepth0PastColor: executionCells.Depth0Past!,
			ExecutionDepth1PastColor: executionCells.Depth1Past!,
			ExecutionDepth2PastColor: executionCells.Depth2Past!,
			ExecutionDepth3PastColor: executionCells.Depth3Past!,
			ExecutionCurrentStepMarkerColor: executionCells.CurrentStepMarker!,
			InfoColor: dto.Chrome?.Info ?? defaults.InfoColor,
			ConnectedColor: dto.Chrome?.Connected ?? defaults.ConnectedColor,
			DisconnectedColor: dto.Chrome?.Disconnected ?? defaults.DisconnectedColor,
			PanelBackgroundColor: dto.Chrome?.PanelBackground ?? defaults.PanelBackgroundColor,
			PanelHeaderBackgroundColor: dto.Chrome?.PanelHeaderBackground ?? defaults.PanelHeaderBackgroundColor,
			SubtleBorderColor: dto.Chrome?.SubtleBorder ?? defaults.SubtleBorderColor,
			SeparatorColor: dto.Chrome?.Separator ?? defaults.SeparatorColor,
			SecondaryForegroundColor: dto.Chrome?.SecondaryForeground ?? defaults.SecondaryForegroundColor,
			GridBorderColor: dto.Chrome?.GridBorder ?? defaults.GridBorderColor,
			GridBackgroundColor: dto.Chrome?.GridBackground ?? defaults.GridBackgroundColor,
			HeaderForegroundColor: dto.Chrome?.HeaderForeground ?? defaults.HeaderForegroundColor);
	}
}
