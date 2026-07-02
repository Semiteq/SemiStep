using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Mapping;

internal static class AppUiOptionsMapper
{
	public static AppUiOptions Map(AppUiOptionsDto? dto)
	{
		if (dto?.Locale is not { } locale || string.IsNullOrWhiteSpace(locale))
		{
			return AppUiOptions.Default;
		}

		return new AppUiOptions(locale.Trim());
	}
}
