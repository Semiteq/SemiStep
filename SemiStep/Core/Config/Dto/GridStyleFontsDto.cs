using YamlDotNet.Serialization;

namespace Config.Dto;

internal sealed class GridStyleFontsDto
{
	[YamlMember(Alias = "header_size")] public int? HeaderSize { get; set; }

	[YamlMember(Alias = "cell_size")] public int? CellSize { get; set; }
}
