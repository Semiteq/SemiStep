using Config.Dto;

using TypesShared.Style;

namespace Config.Mapping;

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
			NormalForegroundColor: dto.Colors?.Cells?.NormalForeground ?? defaults.NormalForegroundColor,
			EnabledCellNormalColor: dto.Colors?.Cells?.Enabled?.Normal ?? defaults.EnabledCellNormalColor,
			EnabledCellSelectedColor: dto.Colors?.Cells?.Enabled?.Selected ?? defaults.EnabledCellSelectedColor,
			ReadonlyCellNormalColor: dto.Colors?.Cells?.Readonly?.Normal ?? defaults.ReadonlyCellNormalColor,
			ReadonlyCellSelectedColor: dto.Colors?.Cells?.Readonly?.Selected ?? defaults.ReadonlyCellSelectedColor,
			DisabledCellNormalColor: dto.Colors?.Cells?.Disabled?.Normal ?? defaults.DisabledCellNormalColor,
			DisabledCellSelectedColor: dto.Colors?.Cells?.Disabled?.Selected ?? defaults.DisabledCellSelectedColor,
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
			ValidationPanelMaxHeight: dto.ValidationPanel?.MaxHeight ?? defaults.ValidationPanelMaxHeight);
	}
}
