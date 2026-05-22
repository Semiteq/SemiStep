using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleReadOnlyCellColorsDto
{
	[YamlMember(Alias = "depth_0")] public string? Depth0 { get; set; }

	[YamlMember(Alias = "depth_1")] public string? Depth1 { get; set; }

	[YamlMember(Alias = "depth_2")] public string? Depth2 { get; set; }

	[YamlMember(Alias = "depth_3")] public string? Depth3 { get; set; }

	[YamlMember(Alias = "depth_0_past")] public string? Depth0Past { get; set; }

	[YamlMember(Alias = "depth_1_past")] public string? Depth1Past { get; set; }

	[YamlMember(Alias = "depth_2_past")] public string? Depth2Past { get; set; }

	[YamlMember(Alias = "depth_3_past")] public string? Depth3Past { get; set; }

	[YamlMember(Alias = "selected")] public string? Selected { get; set; }

	[YamlMember(Alias = "foreground")] public string? Foreground { get; set; }
}
