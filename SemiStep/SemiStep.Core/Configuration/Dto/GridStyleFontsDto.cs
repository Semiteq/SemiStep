using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleFontsDto
{
	[YamlMember(Alias = "family", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Family { get; set; }

	[YamlMember(Alias = "header_size")] public int? HeaderSize { get; set; }

	[YamlMember(Alias = "header_weight")] public int? HeaderWeight { get; set; }

	[YamlMember(Alias = "header_italic")] public bool? HeaderItalic { get; set; }

	[YamlMember(Alias = "cell_size")] public int? CellSize { get; set; }

	[YamlMember(Alias = "cell_weight")] public int? CellWeight { get; set; }

	[YamlMember(Alias = "cell_italic")] public bool? CellItalic { get; set; }
}
