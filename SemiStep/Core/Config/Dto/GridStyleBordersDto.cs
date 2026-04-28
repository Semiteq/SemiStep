using YamlDotNet.Serialization;

namespace Config.Dto;

internal sealed class GridStyleBordersDto
{
	[YamlMember(Alias = "grid_line_thickness")]
	public double? GridLineThickness { get; set; }
}
