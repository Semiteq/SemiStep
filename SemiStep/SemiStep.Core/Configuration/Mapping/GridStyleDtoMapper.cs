using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Mapping;

internal static class GridStyleDtoMapper
{
	public static GridStyleOptionsDto Map(GridStyleOptions options)
	{
		return new GridStyleOptionsDto
		{
			Fonts = new GridStyleFontsDto
			{
				Family = options.Fonts.FontFamily,
				HeaderSize = options.Fonts.HeaderFontSize,
				HeaderWeight = options.Fonts.HeaderFontWeight,
				HeaderItalic = options.Fonts.HeaderItalic,
				CellSize = options.Fonts.CellFontSize,
				CellWeight = options.Fonts.CellFontWeight,
				CellItalic = options.Fonts.CellItalic
			},
			Layout = new GridStyleLayoutDto
			{
				CellPaddingLeft = options.Layout.CellPaddingLeft,
				CellPaddingTop = options.Layout.CellPaddingTop,
				CellPaddingRight = options.Layout.CellPaddingRight,
				CellPaddingBottom = options.Layout.CellPaddingBottom,
				RowHeight = options.Layout.RowHeight
			},
			Colors = new GridStyleColorsDto
			{
				Selection = new GridStyleSelectionColorsDto
				{
					Background = options.Selection.Background,
					Foreground = options.Selection.Foreground
				},
				Cells = new GridStyleCellColorsDto
				{
					Changed = options.ChangedCells.Changed,
					ChangedSelected = options.ChangedCells.ChangedSelected,
					ReadOnly = new GridStyleReadOnlyCellColorsDto
					{
						Depth0 = options.ReadOnlyCells.Depth0,
						Depth1 = options.ReadOnlyCells.Depth1,
						Depth2 = options.ReadOnlyCells.Depth2,
						Depth3 = options.ReadOnlyCells.Depth3,
						Depth0Past = options.ReadOnlyCells.Depth0Past,
						Depth1Past = options.ReadOnlyCells.Depth1Past,
						Depth2Past = options.ReadOnlyCells.Depth2Past,
						Depth3Past = options.ReadOnlyCells.Depth3Past,
						Selected = options.ReadOnlyCells.Selected,
						Foreground = options.ReadOnlyCells.Foreground
					},
					Disabled = new GridStyleDisabledCellColorsDto
					{
						Depth0 = options.DisabledCells.Depth0,
						Depth1 = options.DisabledCells.Depth1,
						Depth2 = options.DisabledCells.Depth2,
						Depth3 = options.DisabledCells.Depth3,
						Depth0Past = options.DisabledCells.Depth0Past,
						Depth1Past = options.DisabledCells.Depth1Past,
						Depth2Past = options.DisabledCells.Depth2Past,
						Depth3Past = options.DisabledCells.Depth3Past,
						Selected = options.DisabledCells.Selected,
						Foreground = options.DisabledCells.Foreground
					},
					Execution = new GridStyleExecutionColorsDto
					{
						Depth0 = options.Execution.Depth0,
						Depth1 = options.Execution.Depth1,
						Depth2 = options.Execution.Depth2,
						Depth3 = options.Execution.Depth3,
						Depth0Past = options.Execution.Depth0Past,
						Depth1Past = options.Execution.Depth1Past,
						Depth2Past = options.Execution.Depth2Past,
						Depth3Past = options.Execution.Depth3Past,
						CurrentStepMarker = options.Execution.CurrentStepMarker
					}
				},
				GridLine = options.Chrome.GridLine
			},
			StatusBar = new StatusBarStyleDto
			{
				Background = options.StatusBar.Background,
				Foreground = options.StatusBar.Foreground,
				Padding = options.StatusBar.Padding,
				ItemSpacing = options.StatusBar.ItemSpacing,
				FontSize = options.StatusBar.FontSize,
				Weight = options.StatusBar.Weight,
				Italic = options.StatusBar.Italic,
				TimerLabelFontSize = options.StatusBar.TimerLabelFontSize,
				TimerLabelWeight = options.StatusBar.TimerLabelWeight,
				TimerLabelItalic = options.StatusBar.TimerLabelItalic,
				TimerValueFontSize = options.StatusBar.TimerValueFontSize,
				TimerValueWeight = options.StatusBar.TimerValueWeight,
				TimerValueItalic = options.StatusBar.TimerValueItalic
			},
			ValidationPanel = new ValidationPanelStyleDto
			{
				Background = options.ValidationPanel.Background,
				Foreground = options.ValidationPanel.Foreground,
				ErrorColor = options.ValidationPanel.ErrorColor,
				WarningColor = options.ValidationPanel.WarningColor,
				MaxHeight = options.ValidationPanel.MaxHeight
			},
			Chrome = new GridStyleChromeColorsDto
			{
				Info = options.Chrome.Info,
				Connected = options.Chrome.Connected,
				Disconnected = options.Chrome.Disconnected,
				LocalMode = options.Chrome.LocalMode,
				Connecting = options.Chrome.Connecting,
				PanelBackground = options.Chrome.PanelBackground,
				PanelHeaderBackground = options.Chrome.PanelHeaderBackground,
				SubtleBorder = options.Chrome.SubtleBorder,
				Separator = options.Chrome.Separator,
				SecondaryForeground = options.Chrome.SecondaryForeground,
				GridBorder = options.Chrome.GridBorder,
				GridBackground = options.Chrome.GridBackground,
				HeaderForeground = options.Chrome.HeaderForeground
			},
			Orientation = GridOrientationValues.Serialize(options.Orientation)
		};
	}
}
