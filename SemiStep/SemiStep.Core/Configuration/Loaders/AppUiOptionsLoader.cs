using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Loaders;

internal static class AppUiOptionsLoader
{
	private static readonly IReadOnlyList<string> _acceptedLocales = ["ru", "en"];

	public static async Task<Result<AppUiOptions>> LoadAsync(string configDirectory)
	{
		var filePath = Path.Combine(configDirectory, ConfigFolderPolicy.UiFolder, ConfigFolderPolicy.AppFileName);

		if (!File.Exists(filePath))
		{
			return Result.Fail<AppUiOptions>(new AppFileNotFoundError(filePath));
		}

		AppUiOptionsDto? dto;

		try
		{
			var content = await File.ReadAllTextAsync(filePath);
			dto = YamlConfigDeserializer.Instance.Deserialize<AppUiOptionsDto?>(content);
		}
		catch (Exception ex)
		{
			return Result.Fail($"Failed to load {ConfigFolderPolicy.AppFileName}: {ex.Message}");
		}

		return ValidateLocale(dto?.Locale);
	}

	private static Result<AppUiOptions> ValidateLocale(string? rawLocale)
	{
		if (string.IsNullOrWhiteSpace(rawLocale))
		{
			return Result.Fail<AppUiOptions>(
				new MissingKeyError(AppUiOptionsDto.LocaleKey, ConfigFolderPolicy.AppFileName));
		}

		var trimmedLocale = rawLocale.Trim();
		var locale = trimmedLocale.ToLowerInvariant();

		return _acceptedLocales.Contains(locale)
			? Result.Ok(new AppUiOptions(locale))
			: Result.Fail<AppUiOptions>(
				new UnsupportedLocaleError(trimmedLocale, ConfigFolderPolicy.AppFileName, _acceptedLocales));
	}
}
