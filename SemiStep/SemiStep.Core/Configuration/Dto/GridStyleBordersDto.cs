using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleBordersDto
{
	[YamlMember(Alias = "grid_line_thickness")]
	public double? GridLineThickness { get; set; }
}
