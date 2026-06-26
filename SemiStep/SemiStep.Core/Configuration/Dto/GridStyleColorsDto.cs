using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleColorsDto
{
	[YamlMember(Alias = "selection")] public GridStyleSelectionColorsDto? Selection { get; set; }

	[YamlMember(Alias = "cells")] public GridStyleCellColorsDto? Cells { get; set; }

	[YamlMember(Alias = "grid_line", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? GridLine { get; set; }
}
