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
					Background = options.Selection.Background.ToString(),
					Foreground = options.Selection.Foreground.ToString()
				},
				Cells = new GridStyleCellColorsDto
				{
					Changed = options.ChangedCells.Changed.ToString(),
					ChangedSelected = options.ChangedCells.ChangedSelected.ToString(),
					ReadOnly = new GridStyleReadOnlyCellColorsDto
					{
						Depth0 = options.ReadOnlyCells.Depth0.ToString(),
						Depth1 = options.ReadOnlyCells.Depth1.ToString(),
						Depth2 = options.ReadOnlyCells.Depth2.ToString(),
						Depth3 = options.ReadOnlyCells.Depth3.ToString(),
						Depth0Past = options.ReadOnlyCells.Depth0Past.ToString(),
						Depth1Past = options.ReadOnlyCells.Depth1Past.ToString(),
						Depth2Past = options.ReadOnlyCells.Depth2Past.ToString(),
						Depth3Past = options.ReadOnlyCells.Depth3Past.ToString(),
						Selected = options.ReadOnlyCells.Selected.ToString(),
						Foreground = options.ReadOnlyCells.Foreground.ToString()
					},
					Disabled = new GridStyleDisabledCellColorsDto
					{
						Depth0 = options.DisabledCells.Depth0.ToString(),
						Depth1 = options.DisabledCells.Depth1.ToString(),
						Depth2 = options.DisabledCells.Depth2.ToString(),
						Depth3 = options.DisabledCells.Depth3.ToString(),
						Depth0Past = options.DisabledCells.Depth0Past.ToString(),
						Depth1Past = options.DisabledCells.Depth1Past.ToString(),
						Depth2Past = options.DisabledCells.Depth2Past.ToString(),
						Depth3Past = options.DisabledCells.Depth3Past.ToString(),
						Selected = options.DisabledCells.Selected.ToString(),
						Foreground = options.DisabledCells.Foreground.ToString()
					},
					Execution = new GridStyleExecutionColorsDto
					{
						Depth0 = options.Execution.Depth0.ToString(),
						Depth1 = options.Execution.Depth1.ToString(),
						Depth2 = options.Execution.Depth2.ToString(),
						Depth3 = options.Execution.Depth3.ToString(),
						Depth0Past = options.Execution.Depth0Past.ToString(),
						Depth1Past = options.Execution.Depth1Past.ToString(),
						Depth2Past = options.Execution.Depth2Past.ToString(),
						Depth3Past = options.Execution.Depth3Past.ToString(),
						CurrentStepMarker = options.Execution.CurrentStepMarker.ToString()
					}
				},
				GridLine = options.Chrome.GridLine.ToString()
			},
			StatusBar = new StatusBarStyleDto
			{
				Background = options.StatusBar.Background.ToString(),
				Foreground = options.StatusBar.Foreground.ToString(),
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
				Background = options.ValidationPanel.Background.ToString(),
				Foreground = options.ValidationPanel.Foreground.ToString(),
				ErrorColor = options.ValidationPanel.ErrorColor.ToString(),
				WarningColor = options.ValidationPanel.WarningColor.ToString(),
				MaxHeight = options.ValidationPanel.MaxHeight
			},
			Chrome = new GridStyleChromeColorsDto
			{
				Info = options.Chrome.Info.ToString(),
				Connected = options.Chrome.Connected.ToString(),
				Disconnected = options.Chrome.Disconnected.ToString(),
				LocalMode = options.Chrome.LocalMode.ToString(),
				Connecting = options.Chrome.Connecting.ToString(),
				PanelBackground = options.Chrome.PanelBackground.ToString(),
				PanelHeaderBackground = options.Chrome.PanelHeaderBackground.ToString(),
				SubtleBorder = options.Chrome.SubtleBorder.ToString(),
				Separator = options.Chrome.Separator.ToString(),
				SecondaryForeground = options.Chrome.SecondaryForeground.ToString(),
				GridBorder = options.Chrome.GridBorder.ToString(),
				GridBackground = options.Chrome.GridBackground.ToString(),
				HeaderForeground = options.Chrome.HeaderForeground.ToString()
			},
			Orientation = GridOrientationValues.Serialize(options.Orientation)
		};
	}
}
