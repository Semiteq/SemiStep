using SemiStep.Core.Configuration;
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
		// Validator-first invariant: GridStyleValidator runs before this mapper and rejects
		// any DTO where `colors.cells.disabled` or `colors.execution` (or any of their hex
		// keys) is missing. The local aliases below let us read each field without re-
		// suppressing the chain on every line.
		var disabled = dto.Colors!.Cells!.Disabled!;
		var execution = dto.Colors!.Execution!;

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
			DisabledCellDepth0Color: disabled.Depth0!,
			DisabledCellDepth1Color: disabled.Depth1!,
			DisabledCellDepth2Color: disabled.Depth2!,
			DisabledCellDepth3Color: disabled.Depth3!,
			DisabledCellDepth0PastColor: disabled.Depth0Past!,
			DisabledCellDepth1PastColor: disabled.Depth1Past!,
			DisabledCellDepth2PastColor: disabled.Depth2Past!,
			DisabledCellDepth3PastColor: disabled.Depth3Past!,
			DisabledCellSelectedColor: disabled.Selected!,
			DisabledCellForegroundColor: disabled.Foreground!,
			AlternatingRowBackgroundColor: dto.Colors?.Rows?.AlternatingBackground ??
										   defaults.AlternatingRowBackgroundColor,
			NormalRowBackgroundColor: dto.Colors?.Rows?.NormalBackground ?? defaults.NormalRowBackgroundColor,
			GridLineThickness: dto.Borders?.GridLineThickness ?? defaults.GridLineThickness,
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
			ExecutionDepth0Color: execution.Depth0!,
			ExecutionDepth1Color: execution.Depth1!,
			ExecutionDepth2Color: execution.Depth2!,
			ExecutionDepth3Color: execution.Depth3!,
			ExecutionDepth0PastColor: execution.Depth0Past!,
			ExecutionDepth1PastColor: execution.Depth1Past!,
			ExecutionDepth2PastColor: execution.Depth2Past!,
			ExecutionDepth3PastColor: execution.Depth3Past!,
			ExecutionCurrentStepMarkerColor: execution.CurrentStepMarker!);
	}
}
