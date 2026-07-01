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
				Family = options.FontFamily,
				HeaderSize = options.HeaderFontSize,
				HeaderWeight = options.HeaderFontWeight,
				HeaderItalic = options.HeaderItalic,
				CellSize = options.CellFontSize,
				CellWeight = options.CellFontWeight,
				CellItalic = options.CellItalic
			},
			Layout = new GridStyleLayoutDto
			{
				CellPaddingLeft = options.CellPaddingLeft,
				CellPaddingTop = options.CellPaddingTop,
				CellPaddingRight = options.CellPaddingRight,
				CellPaddingBottom = options.CellPaddingBottom,
				RowHeight = options.RowHeight
			},
			Colors = new GridStyleColorsDto
			{
				Selection = new GridStyleSelectionColorsDto
				{
					Background = options.SelectionBackgroundColor,
					Foreground = options.SelectionForegroundColor
				},
				Cells = new GridStyleCellColorsDto
				{
					Changed = options.CellChangedColor,
					ChangedSelected = options.CellChangedSelectedColor,
					ReadOnly = new GridStyleReadOnlyCellColorsDto
					{
						Depth0 = options.ReadOnlyCellDepth0Color,
						Depth1 = options.ReadOnlyCellDepth1Color,
						Depth2 = options.ReadOnlyCellDepth2Color,
						Depth3 = options.ReadOnlyCellDepth3Color,
						Depth0Past = options.ReadOnlyCellDepth0PastColor,
						Depth1Past = options.ReadOnlyCellDepth1PastColor,
						Depth2Past = options.ReadOnlyCellDepth2PastColor,
						Depth3Past = options.ReadOnlyCellDepth3PastColor,
						Selected = options.ReadOnlyCellSelectedColor,
						Foreground = options.ReadOnlyCellForegroundColor
					},
					Disabled = new GridStyleDisabledCellColorsDto
					{
						Depth0 = options.DisabledCellDepth0Color,
						Depth1 = options.DisabledCellDepth1Color,
						Depth2 = options.DisabledCellDepth2Color,
						Depth3 = options.DisabledCellDepth3Color,
						Depth0Past = options.DisabledCellDepth0PastColor,
						Depth1Past = options.DisabledCellDepth1PastColor,
						Depth2Past = options.DisabledCellDepth2PastColor,
						Depth3Past = options.DisabledCellDepth3PastColor,
						Selected = options.DisabledCellSelectedColor,
						Foreground = options.DisabledCellForegroundColor
					},
					Execution = new GridStyleExecutionColorsDto
					{
						Depth0 = options.ExecutionDepth0Color,
						Depth1 = options.ExecutionDepth1Color,
						Depth2 = options.ExecutionDepth2Color,
						Depth3 = options.ExecutionDepth3Color,
						Depth0Past = options.ExecutionDepth0PastColor,
						Depth1Past = options.ExecutionDepth1PastColor,
						Depth2Past = options.ExecutionDepth2PastColor,
						Depth3Past = options.ExecutionDepth3PastColor,
						CurrentStepMarker = options.ExecutionCurrentStepMarkerColor
					}
				},
				GridLine = options.GridLineColor
			},
			StatusBar = new StatusBarStyleDto
			{
				Background = options.StatusBarBackgroundColor,
				Foreground = options.StatusBarForegroundColor,
				Padding = options.StatusBarPadding,
				ItemSpacing = options.StatusBarItemSpacing,
				FontSize = options.StatusBarFontSize,
				Weight = options.StatusBarFontWeight,
				Italic = options.StatusBarItalic,
				TimerLabelFontSize = options.StatusBarTimerLabelFontSize,
				TimerLabelWeight = options.StatusBarTimerLabelFontWeight,
				TimerLabelItalic = options.StatusBarTimerLabelItalic,
				TimerValueFontSize = options.StatusBarTimerValueFontSize,
				TimerValueWeight = options.StatusBarTimerValueFontWeight,
				TimerValueItalic = options.StatusBarTimerValueItalic
			},
			ValidationPanel = new ValidationPanelStyleDto
			{
				Background = options.ValidationPanelBackgroundColor,
				Foreground = options.ValidationPanelForegroundColor,
				ErrorColor = options.ValidationPanelErrorColor,
				WarningColor = options.ValidationPanelWarningColor,
				MaxHeight = options.ValidationPanelMaxHeight
			},
			Chrome = new GridStyleChromeColorsDto
			{
				Info = options.InfoColor,
				Connected = options.ConnectedColor,
				Disconnected = options.DisconnectedColor,
				LocalMode = options.LocalModeColor,
				Connecting = options.ConnectingColor,
				PanelBackground = options.PanelBackgroundColor,
				PanelHeaderBackground = options.PanelHeaderBackgroundColor,
				SubtleBorder = options.SubtleBorderColor,
				Separator = options.SeparatorColor,
				SecondaryForeground = options.SecondaryForegroundColor,
				GridBorder = options.GridBorderColor,
				GridBackground = options.GridBackgroundColor,
				HeaderForeground = options.HeaderForegroundColor
			}
		};
	}
}
