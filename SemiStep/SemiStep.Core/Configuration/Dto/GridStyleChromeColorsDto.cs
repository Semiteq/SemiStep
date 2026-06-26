using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleChromeColorsDto
{
	[YamlMember(Alias = "info", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Info { get; set; }

	[YamlMember(Alias = "connected", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Connected { get; set; }

	[YamlMember(Alias = "disconnected", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Disconnected { get; set; }

	[YamlMember(Alias = "panel_background", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? PanelBackground { get; set; }

	[YamlMember(Alias = "panel_header_background", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? PanelHeaderBackground { get; set; }

	[YamlMember(Alias = "subtle_border", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? SubtleBorder { get; set; }

	[YamlMember(Alias = "separator", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Separator { get; set; }

	[YamlMember(Alias = "secondary_foreground", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? SecondaryForeground { get; set; }

	[YamlMember(Alias = "grid_border", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? GridBorder { get; set; }

	[YamlMember(Alias = "grid_background", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? GridBackground { get; set; }

	[YamlMember(Alias = "header_foreground", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? HeaderForeground { get; set; }
}
