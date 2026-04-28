using YamlDotNet.Serialization;

namespace Config.Dto;

internal sealed class GridStyleColorsDto
{
	[YamlMember(Alias = "selection")] public GridStyleSelectionColorsDto? Selection { get; set; }

	[YamlMember(Alias = "cells")] public GridStyleCellColorsDto? Cells { get; set; }

	[YamlMember(Alias = "rows")] public GridStyleRowColorsDto? Rows { get; set; }

	[YamlMember(Alias = "grid_line")] public string? GridLine { get; set; }
}
