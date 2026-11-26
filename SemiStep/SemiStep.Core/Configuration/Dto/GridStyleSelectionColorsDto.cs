using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleSelectionColorsDto
{
	[YamlMember(Alias = "accent", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Accent { get; set; }

	[YamlMember(Alias = "weight")] public double? Weight { get; set; }
}
