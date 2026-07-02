using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class AppUiOptionsDto
{
	[YamlMember(Alias = "locale")] public string? Locale { get; set; }
}
