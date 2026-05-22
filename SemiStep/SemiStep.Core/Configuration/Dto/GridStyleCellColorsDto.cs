using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleCellColorsDto
{
	[YamlMember(Alias = "readonly")] public GridStyleReadOnlyCellColorsDto? ReadOnly { get; set; }

	[YamlMember(Alias = "disabled")] public GridStyleDisabledCellColorsDto? Disabled { get; set; }

	[YamlMember(Alias = "execution")] public GridStyleExecutionColorsDto? Execution { get; set; }
}
