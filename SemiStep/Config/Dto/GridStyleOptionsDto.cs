using YamlDotNet.Serialization;

namespace Config.Dto;

public sealed class GridStyleOptionsDto
{
	[YamlMember(Alias = "fonts")]
	public GridStyleFontsDto? Fonts { get; set; }

	[YamlMember(Alias = "layout")]
	public GridStyleLayoutDto? Layout { get; set; }

	[YamlMember(Alias = "colors")]
	public GridStyleColorsDto? Colors { get; set; }

	[YamlMember(Alias = "borders")]
	public GridStyleBordersDto? Borders { get; set; }

	[YamlMember(Alias = "status_bar")]
	public StatusBarStyleDto? StatusBar { get; set; }

	[YamlMember(Alias = "validation_panel")]
	public ValidationPanelStyleDto? ValidationPanel { get; set; }
}

public sealed class GridStyleFontsDto
{
	[YamlMember(Alias = "header_size")]
	public int? HeaderSize { get; set; }

	[YamlMember(Alias = "cell_size")]
	public int? CellSize { get; set; }
}

public sealed class GridStyleLayoutDto
{
	[YamlMember(Alias = "cell_padding_left")]
	public double? CellPaddingLeft { get; set; }

	[YamlMember(Alias = "cell_padding_top")]
	public double? CellPaddingTop { get; set; }

	[YamlMember(Alias = "cell_padding_right")]
	public double? CellPaddingRight { get; set; }

	[YamlMember(Alias = "cell_padding_bottom")]
	public double? CellPaddingBottom { get; set; }

	[YamlMember(Alias = "row_height")]
	public double? RowHeight { get; set; }
}

public sealed class GridStyleColorsDto
{
	[YamlMember(Alias = "selection")]
	public GridStyleSelectionColorsDto? Selection { get; set; }

	[YamlMember(Alias = "cells")]
	public GridStyleCellColorsDto? Cells { get; set; }

	[YamlMember(Alias = "rows")]
	public GridStyleRowColorsDto? Rows { get; set; }

	[YamlMember(Alias = "grid_line")]
	public string? GridLine { get; set; }
}

public sealed class GridStyleSelectionColorsDto
{
	[YamlMember(Alias = "background")]
	public string? Background { get; set; }

	[YamlMember(Alias = "foreground")]
	public string? Foreground { get; set; }
}

public sealed class GridStyleCellColorsDto
{
	[YamlMember(Alias = "enabled")]
	public GridStyleCellStateColorsDto? Enabled { get; set; }

	[YamlMember(Alias = "readonly")]
	public GridStyleCellStateColorsDto? Readonly { get; set; }

	[YamlMember(Alias = "disabled")]
	public GridStyleCellStateColorsDto? Disabled { get; set; }

	[YamlMember(Alias = "normal_foreground")]
	public string? NormalForeground { get; set; }
}

public sealed class GridStyleCellStateColorsDto
{
	[YamlMember(Alias = "normal")]
	public string? Normal { get; set; }

	[YamlMember(Alias = "selected")]
	public string? Selected { get; set; }
}

public sealed class GridStyleRowColorsDto
{
	[YamlMember(Alias = "alternating_background")]
	public string? AlternatingBackground { get; set; }

	[YamlMember(Alias = "normal_background")]
	public string? NormalBackground { get; set; }
}

public sealed class GridStyleBordersDto
{
	[YamlMember(Alias = "grid_line_thickness")]
	public double? GridLineThickness { get; set; }
}

public sealed class StatusBarStyleDto
{
	[YamlMember(Alias = "background")]
	public string? Background { get; set; }

	[YamlMember(Alias = "foreground")]
	public string? Foreground { get; set; }

	[YamlMember(Alias = "padding")]
	public double? Padding { get; set; }

	[YamlMember(Alias = "item_spacing")]
	public double? ItemSpacing { get; set; }
}

public sealed class ValidationPanelStyleDto
{
	[YamlMember(Alias = "background")]
	public string? Background { get; set; }

	[YamlMember(Alias = "foreground")]
	public string? Foreground { get; set; }

	[YamlMember(Alias = "error_color")]
	public string? ErrorColor { get; set; }

	[YamlMember(Alias = "warning_color")]
	public string? WarningColor { get; set; }

	[YamlMember(Alias = "max_height")]
	public double? MaxHeight { get; set; }
}
