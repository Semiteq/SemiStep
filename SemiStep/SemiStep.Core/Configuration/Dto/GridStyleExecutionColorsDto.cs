using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class GridStyleExecutionColorsDto
{
	[YamlMember(Alias = "running", ScalarStyle = ScalarStyle.DoubleQuoted)]
	public string? Running { get; set; }

	[YamlMember(Alias = "running_weight")] public double? RunningWeight { get; set; }

	[YamlMember(Alias = "past_weight")] public double? PastWeight { get; set; }
}
