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
					Accent = options.Selection.Accent.ToString(),
					Weight = options.Selection.Weight
				},
				Cells = new GridStyleCellColorsDto
				{
					Changed = options.ChangedCells.Changed.ToString(),
					ReadOnly = new GridStyleGreyCellColorsDto { Depth0 = options.ReadOnlyCell.ToString() },
					Disabled = new GridStyleGreyCellColorsDto { Depth0 = options.DisabledCell.ToString() },
					Execution = new GridStyleExecutionColorsDto
					{
						Running = options.Execution.Running.ToString(),
						RunningWeight = options.Execution.RunningWeight,
						PastWeight = options.Execution.PastWeight
					},
					Loop = new GridStyleLoopColorsDto
					{
						Color = options.Loop.Color.ToString(),
						Depth1Weight = options.Loop.Depth1Weight,
						Depth2Weight = options.Loop.Depth2Weight,
						Depth3Weight = options.Loop.Depth3Weight
					}
				},
				GridLine = options.Chrome.GridLine.ToString()
			},
			StatusBar = new StatusBarStyleDto
			{
				Background = options.StatusBar.Background.ToString(),
				Foreground = options.StatusBar.Foreground.ToString(),
				Padding = options.StatusBar.Padding,
				FontSize = options.StatusBar.FontSize,
				Weight = options.StatusBar.Weight,
				Italic = options.StatusBar.Italic
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
