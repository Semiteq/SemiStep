using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class ValidationPanelStyleDto
{
	[YamlMember(Alias = "background", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Background { get; set; }

	[YamlMember(Alias = "foreground", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Foreground { get; set; }

	[YamlMember(Alias = "error_color", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? ErrorColor { get; set; }

	[YamlMember(Alias = "warning_color", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? WarningColor { get; set; }

	[YamlMember(Alias = "max_height")] public double? MaxHeight { get; set; }
}
