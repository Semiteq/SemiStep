using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class StatusBarStyleDto
{
	[YamlMember(Alias = "background")] public string? Background { get; set; }

	[YamlMember(Alias = "foreground")] public string? Foreground { get; set; }

	[YamlMember(Alias = "padding")] public double? Padding { get; set; }

	[YamlMember(Alias = "item_spacing")] public double? ItemSpacing { get; set; }
}
