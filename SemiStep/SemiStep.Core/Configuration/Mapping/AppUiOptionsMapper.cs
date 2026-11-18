using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Mapping;

internal static class AppUiOptionsMapper
{
	public static AppUiOptions Map(AppUiOptionsDto? dto)
	{
		return dto?.Locale is not { } locale || string.IsNullOrWhiteSpace(locale) ? AppUiOptions.Default : new AppUiOptions(locale.Trim());
	}
}
