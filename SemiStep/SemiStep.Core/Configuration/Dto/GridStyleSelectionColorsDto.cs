using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleSelectionColorsDto
{
	[YamlMember(Alias = "background", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Background { get; set; }

	[YamlMember(Alias = "foreground", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Foreground { get; set; }
}
