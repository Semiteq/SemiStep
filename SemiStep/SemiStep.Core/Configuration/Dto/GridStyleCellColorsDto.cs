using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleCellColorsDto
{
	[YamlMember(Alias = "changed", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Changed { get; set; }

	[YamlMember(Alias = "changed_selected", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? ChangedSelected { get; set; }

	[YamlMember(Alias = "readonly")] public GridStyleReadOnlyCellColorsDto? ReadOnly { get; set; }

	[YamlMember(Alias = "disabled")] public GridStyleDisabledCellColorsDto? Disabled { get; set; }

	[YamlMember(Alias = "execution")] public GridStyleExecutionColorsDto? Execution { get; set; }
}
