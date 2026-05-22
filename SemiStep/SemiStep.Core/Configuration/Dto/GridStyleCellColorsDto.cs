using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleCellColorsDto
{
	[YamlMember(Alias = "disabled")] public GridStyleDisabledCellColorsDto? Disabled { get; set; }
}
