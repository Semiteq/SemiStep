using YamlDotNet.Serialization;

namespace Config.Dto;

internal sealed class ValidationPanelStyleDto
{
	[YamlMember(Alias = "background")] public string? Background { get; set; }

	[YamlMember(Alias = "foreground")] public string? Foreground { get; set; }

	[YamlMember(Alias = "error_color")] public string? ErrorColor { get; set; }

	[YamlMember(Alias = "warning_color")] public string? WarningColor { get; set; }

	[YamlMember(Alias = "max_height")] public double? MaxHeight { get; set; }
}
