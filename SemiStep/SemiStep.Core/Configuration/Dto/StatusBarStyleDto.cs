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

	[YamlMember(Alias = "font_size")] public int? FontSize { get; set; }

	[YamlMember(Alias = "weight")] public int? Weight { get; set; }

	[YamlMember(Alias = "italic")] public bool? Italic { get; set; }

}
