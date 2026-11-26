using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleLoopColorsDto
{
	[YamlMember(Alias = "color", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Color { get; set; }

	[YamlMember(Alias = "depth_1_weight")] public double? Depth1Weight { get; set; }

	[YamlMember(Alias = "depth_2_weight")] public double? Depth2Weight { get; set; }

	[YamlMember(Alias = "depth_3_weight")] public double? Depth3Weight { get; set; }
}
