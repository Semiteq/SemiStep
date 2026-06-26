using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleDisabledCellColorsDto
{
	[YamlMember(Alias = "depth_0", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Depth0 { get; set; }

	[YamlMember(Alias = "depth_1", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Depth1 { get; set; }

	[YamlMember(Alias = "depth_2", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Depth2 { get; set; }

	[YamlMember(Alias = "depth_3", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Depth3 { get; set; }

	[YamlMember(Alias = "depth_0_past", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Depth0Past { get; set; }

	[YamlMember(Alias = "depth_1_past", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Depth1Past { get; set; }

	[YamlMember(Alias = "depth_2_past", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Depth2Past { get; set; }

	[YamlMember(Alias = "depth_3_past", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Depth3Past { get; set; }

	[YamlMember(Alias = "selected", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Selected { get; set; }

	[YamlMember(Alias = "foreground", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Foreground { get; set; }
}
