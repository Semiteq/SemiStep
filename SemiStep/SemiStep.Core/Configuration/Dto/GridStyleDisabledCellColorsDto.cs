using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleDisabledCellColorsDto
{
	[YamlMember(Alias = "normal")] public string? Normal { get; set; }

	[YamlMember(Alias = "selected")] public string? Selected { get; set; }

	[YamlMember(Alias = "foreground")] public string? Foreground { get; set; }
}
