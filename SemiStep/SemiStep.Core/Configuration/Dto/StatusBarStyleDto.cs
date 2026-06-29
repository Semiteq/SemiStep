using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class StatusBarStyleDto
{
	[YamlMember(Alias = "background", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Background { get; set; }

	[YamlMember(Alias = "foreground", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Foreground { get; set; }

	[YamlMember(Alias = "padding")] public double? Padding { get; set; }

	[YamlMember(Alias = "item_spacing")] public double? ItemSpacing { get; set; }

	[YamlMember(Alias = "font_size")] public int? FontSize { get; set; }

	[YamlMember(Alias = "weight")] public int? Weight { get; set; }

	[YamlMember(Alias = "italic")] public bool? Italic { get; set; }

	[YamlMember(Alias = "timer_label_font_size")] public int? TimerLabelFontSize { get; set; }

	[YamlMember(Alias = "timer_label_weight")] public int? TimerLabelWeight { get; set; }

	[YamlMember(Alias = "timer_label_italic")] public bool? TimerLabelItalic { get; set; }

	[YamlMember(Alias = "timer_value_font_size")] public int? TimerValueFontSize { get; set; }

	[YamlMember(Alias = "timer_value_weight")] public int? TimerValueWeight { get; set; }

	[YamlMember(Alias = "timer_value_italic")] public bool? TimerValueItalic { get; set; }
}
