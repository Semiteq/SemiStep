using YamlDotNet.Serialization;

namespace Config.Dto;

internal sealed class GridStyleCellColorsDto
{
	[YamlMember(Alias = "enabled")] public GridStyleCellStateColorsDto? Enabled { get; set; }

	[YamlMember(Alias = "readonly")] public GridStyleCellStateColorsDto? Readonly { get; set; }

	[YamlMember(Alias = "disabled")] public GridStyleCellStateColorsDto? Disabled { get; set; }

	[YamlMember(Alias = "normal_foreground")]
	public string? NormalForeground { get; set; }
}
