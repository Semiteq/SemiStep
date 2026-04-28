using YamlDotNet.Serialization;

namespace Config.Dto;

internal sealed class GridStyleLayoutDto
{
	[YamlMember(Alias = "cell_padding_left")]
	public double? CellPaddingLeft { get; set; }

	[YamlMember(Alias = "cell_padding_top")]
	public double? CellPaddingTop { get; set; }

	[YamlMember(Alias = "cell_padding_right")]
	public double? CellPaddingRight { get; set; }

	[YamlMember(Alias = "cell_padding_bottom")]
	public double? CellPaddingBottom { get; set; }

	[YamlMember(Alias = "row_height")] public double? RowHeight { get; set; }
}
