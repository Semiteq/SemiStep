using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class StatusBarStyleDto
{
	[YamlMember(Alias = "background", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Background { get; set; }

	[YamlMember(Alias = "foreground", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Foreground { get; set; }

	[YamlMember(Alias = "padding")] public double? Padding { get; set; }

	[YamlMember(Alias = "item_spacing")] public double? ItemSpacing { get; set; }
}
