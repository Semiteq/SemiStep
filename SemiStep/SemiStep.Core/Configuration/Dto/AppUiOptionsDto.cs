using YamlDotNet.Serialization;

namespace SemiStep.Core.Configuration.Dto;

internal sealed class AppUiOptionsDto
{
	public const string LocaleKey = "locale";

	[YamlMember(Alias = LocaleKey)] public string? Locale { get; set; }
}
