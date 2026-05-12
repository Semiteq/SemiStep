using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleCellStateColorsDto
{
	[YamlMember(Alias = "normal")] public string? Normal { get; set; }

	[YamlMember(Alias = "selected")] public string? Selected { get; set; }
}
