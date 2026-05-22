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
			// Validator-first invariant: GridStyleValidator runs before this mapper and rejects any
			// DTO whose `colors.cells.disabled.{normal,selected,foreground}` keys are missing or
			// malformed. The null-forgiving operators below are therefore safe.
			DisabledCellNormalColor: dto.Colors!.Cells!.Disabled!.Normal!,
			DisabledCellSelectedBackgroundColor: dto.Colors.Cells.Disabled.Selected!,
			DisabledCellForegroundColor: dto.Colors.Cells.Disabled.Foreground!,
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
			// Validator-first invariant: GridStyleValidator runs before this mapper and rejects
			// any DTO where `colors.execution` (or any of the nine hex keys below) is missing.
			// The null-forgiving operators are therefore safe — by the time the mapper sees the
			// DTO, every execution-palette field is guaranteed to be a non-null hex string.
			ExecutionDepth0Color: dto.Colors!.Execution!.Depth0!,
			ExecutionDepth1Color: dto.Colors.Execution.Depth1!,
			ExecutionDepth2Color: dto.Colors.Execution.Depth2!,
			ExecutionDepth3Color: dto.Colors.Execution.Depth3!,
			ExecutionDepth0PastColor: dto.Colors.Execution.Depth0Past!,
			ExecutionDepth1PastColor: dto.Colors.Execution.Depth1Past!,
			ExecutionDepth2PastColor: dto.Colors.Execution.Depth2Past!,
			ExecutionDepth3PastColor: dto.Colors.Execution.Depth3Past!,
			ExecutionCurrentStepMarkerColor: dto.Colors.Execution.CurrentStepMarker!);
	}
}
