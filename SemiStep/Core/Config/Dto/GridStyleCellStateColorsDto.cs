using YamlDotNet.Serialization;

namespace Config.Dto;

internal sealed class GridStyleCellStateColorsDto
{
	[YamlMember(Alias = "normal")] public string? Normal { get; set; }

	[YamlMember(Alias = "selected")] public string? Selected { get; set; }
}
