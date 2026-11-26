using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleGreyCellColorsDto
{
	[YamlMember(Alias = "depth_0", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Depth0 { get; set; }
}
