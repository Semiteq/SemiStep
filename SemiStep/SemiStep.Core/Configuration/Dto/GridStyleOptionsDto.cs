using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

// Property declaration order controls the key order YamlDotNet emits into saved grid_style.yaml;
// keep "fonts" first and append new keys at the end.
internal sealed class GridStyleOptionsDto
{
	[YamlMember(Alias = "fonts")] public GridStyleFontsDto? Fonts { get; set; }

	[YamlMember(Alias = "layout")] public GridStyleLayoutDto? Layout { get; set; }

	[YamlMember(Alias = "colors")] public GridStyleColorsDto? Colors { get; set; }

	[YamlMember(Alias = "status_bar")] public StatusBarStyleDto? StatusBar { get; set; }

	[YamlMember(Alias = "validation_panel")]
	public ValidationPanelStyleDto? ValidationPanel { get; set; }

	[YamlMember(Alias = "chrome")] public GridStyleChromeColorsDto? Chrome { get; set; }

	[YamlMember(Alias = "orientation")] public string? Orientation { get; set; }
}
