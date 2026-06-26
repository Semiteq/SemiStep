using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleOptionsDto
{
	[YamlMember(Alias = "fonts")] public GridStyleFontsDto? Fonts { get; set; }

	[YamlMember(Alias = "layout")] public GridStyleLayoutDto? Layout { get; set; }

	[YamlMember(Alias = "colors")] public GridStyleColorsDto? Colors { get; set; }

	[YamlMember(Alias = "status_bar")] public StatusBarStyleDto? StatusBar { get; set; }

	[YamlMember(Alias = "validation_panel")]
	public ValidationPanelStyleDto? ValidationPanel { get; set; }

	[YamlMember(Alias = "chrome")] public GridStyleChromeColorsDto? Chrome { get; set; }
}
