using YamlDotNet.Serialization;

namespace Config.Dto;

internal sealed class GridStyleRowColorsDto
{
	[YamlMember(Alias = "alternating_background")]
	public string? AlternatingBackground { get; set; }

	[YamlMember(Alias = "normal_background")]
	public string? NormalBackground { get; set; }
}
