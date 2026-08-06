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
			Fonts: new GridStyleFonts(
				FontFamily: dto.Fonts?.Family ?? defaults.Fonts.FontFamily,
				HeaderFontSize: dto.Fonts?.HeaderSize ?? defaults.Fonts.HeaderFontSize,
				HeaderFontWeight: dto.Fonts?.HeaderWeight ?? defaults.Fonts.HeaderFontWeight,
				HeaderItalic: dto.Fonts?.HeaderItalic ?? defaults.Fonts.HeaderItalic,
				CellFontSize: dto.Fonts?.CellSize ?? defaults.Fonts.CellFontSize,
				CellFontWeight: dto.Fonts?.CellWeight ?? defaults.Fonts.CellFontWeight,
				CellItalic: dto.Fonts?.CellItalic ?? defaults.Fonts.CellItalic),
			Layout: new GridStyleLayout(
				CellPaddingLeft: dto.Layout?.CellPaddingLeft ?? defaults.Layout.CellPaddingLeft,
				CellPaddingTop: dto.Layout?.CellPaddingTop ?? defaults.Layout.CellPaddingTop,
				CellPaddingRight: dto.Layout?.CellPaddingRight ?? defaults.Layout.CellPaddingRight,
				CellPaddingBottom: dto.Layout?.CellPaddingBottom ?? defaults.Layout.CellPaddingBottom,
				RowHeight: dto.Layout?.RowHeight ?? defaults.Layout.RowHeight),
			Selection: new SelectionColors(
				Background: dto.Colors?.Selection?.Background ?? defaults.Selection.Background,
				Foreground: dto.Colors?.Selection?.Foreground ?? defaults.Selection.Foreground),
			ChangedCells: new ChangedCellColors(
				Changed: dto.Colors?.Cells?.Changed ?? defaults.ChangedCells.Changed,
				ChangedSelected: dto.Colors?.Cells?.ChangedSelected ?? defaults.ChangedCells.ChangedSelected),
			ReadOnlyCells: new DepthPalette(
				Depth0: readOnlyCells.Depth0!,
				Depth1: readOnlyCells.Depth1!,
				Depth2: readOnlyCells.Depth2!,
				Depth3: readOnlyCells.Depth3!,
				Depth0Past: readOnlyCells.Depth0Past!,
				Depth1Past: readOnlyCells.Depth1Past!,
				Depth2Past: readOnlyCells.Depth2Past!,
				Depth3Past: readOnlyCells.Depth3Past!,
				Selected: readOnlyCells.Selected!,
				Foreground: readOnlyCells.Foreground!),
			DisabledCells: new DepthPalette(
				Depth0: disabledCells.Depth0!,
				Depth1: disabledCells.Depth1!,
				Depth2: disabledCells.Depth2!,
				Depth3: disabledCells.Depth3!,
				Depth0Past: disabledCells.Depth0Past!,
				Depth1Past: disabledCells.Depth1Past!,
				Depth2Past: disabledCells.Depth2Past!,
				Depth3Past: disabledCells.Depth3Past!,
				Selected: disabledCells.Selected!,
				Foreground: disabledCells.Foreground!),
			Execution: new ExecutionPalette(
				Depth0: executionCells.Depth0!,
				Depth1: executionCells.Depth1!,
				Depth2: executionCells.Depth2!,
				Depth3: executionCells.Depth3!,
				Depth0Past: executionCells.Depth0Past!,
				Depth1Past: executionCells.Depth1Past!,
				Depth2Past: executionCells.Depth2Past!,
				Depth3Past: executionCells.Depth3Past!,
				CurrentStepMarker: executionCells.CurrentStepMarker!),
			StatusBar: new StatusBarStyle(
				Background: dto.StatusBar?.Background ?? defaults.StatusBar.Background,
				Foreground: dto.StatusBar?.Foreground ?? defaults.StatusBar.Foreground,
				Padding: dto.StatusBar?.Padding ?? defaults.StatusBar.Padding,
				ItemSpacing: dto.StatusBar?.ItemSpacing ?? defaults.StatusBar.ItemSpacing,
				FontSize: dto.StatusBar?.FontSize ?? defaults.StatusBar.FontSize,
				Weight: dto.StatusBar?.Weight ?? defaults.StatusBar.Weight,
				Italic: dto.StatusBar?.Italic ?? defaults.StatusBar.Italic,
				TimerLabelFontSize: dto.StatusBar?.TimerLabelFontSize ?? defaults.StatusBar.TimerLabelFontSize,
				TimerLabelWeight: dto.StatusBar?.TimerLabelWeight ?? defaults.StatusBar.TimerLabelWeight,
				TimerLabelItalic: dto.StatusBar?.TimerLabelItalic ?? defaults.StatusBar.TimerLabelItalic,
				TimerValueFontSize: dto.StatusBar?.TimerValueFontSize ?? defaults.StatusBar.TimerValueFontSize,
				TimerValueWeight: dto.StatusBar?.TimerValueWeight ?? defaults.StatusBar.TimerValueWeight,
				TimerValueItalic: dto.StatusBar?.TimerValueItalic ?? defaults.StatusBar.TimerValueItalic),
			ValidationPanel: new ValidationPanelStyle(
				Background: dto.ValidationPanel?.Background ?? defaults.ValidationPanel.Background,
				Foreground: dto.ValidationPanel?.Foreground ?? defaults.ValidationPanel.Foreground,
				ErrorColor: dto.ValidationPanel?.ErrorColor ?? defaults.ValidationPanel.ErrorColor,
				WarningColor: dto.ValidationPanel?.WarningColor ?? defaults.ValidationPanel.WarningColor,
				MaxHeight: dto.ValidationPanel?.MaxHeight ?? defaults.ValidationPanel.MaxHeight),
			Chrome: new ChromeColors(
				Info: dto.Chrome?.Info ?? defaults.Chrome.Info,
				Connected: dto.Chrome?.Connected ?? defaults.Chrome.Connected,
				Disconnected: dto.Chrome?.Disconnected ?? defaults.Chrome.Disconnected,
				LocalMode: dto.Chrome?.LocalMode ?? defaults.Chrome.LocalMode,
				Connecting: dto.Chrome?.Connecting ?? defaults.Chrome.Connecting,
				PanelBackground: dto.Chrome?.PanelBackground ?? defaults.Chrome.PanelBackground,
				PanelHeaderBackground: dto.Chrome?.PanelHeaderBackground ?? defaults.Chrome.PanelHeaderBackground,
				SubtleBorder: dto.Chrome?.SubtleBorder ?? defaults.Chrome.SubtleBorder,
				Separator: dto.Chrome?.Separator ?? defaults.Chrome.Separator,
				SecondaryForeground: dto.Chrome?.SecondaryForeground ?? defaults.Chrome.SecondaryForeground,
				GridBorder: dto.Chrome?.GridBorder ?? defaults.Chrome.GridBorder,
				GridBackground: dto.Chrome?.GridBackground ?? defaults.Chrome.GridBackground,
				HeaderForeground: dto.Chrome?.HeaderForeground ?? defaults.Chrome.HeaderForeground,
				GridLine: dto.Colors?.GridLine ?? defaults.Chrome.GridLine),
			Orientation: GridOrientationValues.Parse(dto.Orientation));
	}
}
