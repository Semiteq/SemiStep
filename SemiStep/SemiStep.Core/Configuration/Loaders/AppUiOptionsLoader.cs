using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Loaders;

internal static class AppUiOptionsLoader
{
	public static async Task<Result<AppUiOptionsDto?>> LoadAsync(string configDirectory)
	{
		var filePath = Path.Combine(configDirectory, ConfigFolderPolicy.UiFolder, ConfigFolderPolicy.AppFileName);

		// Optional file: absence yields defaults, unlike the required grid_style.yaml.
		if (!File.Exists(filePath))
		{
			return Result.Ok<AppUiOptionsDto?>(null);
		}

		try
		{
			var content = await File.ReadAllTextAsync(filePath);

			return Result.Ok(YamlConfigDeserializer.Instance.Deserialize<AppUiOptionsDto?>(content));
		}
		catch (Exception ex)
		{
			return Result.Fail($"Failed to load {Path.GetFileName(filePath)}: {ex.Message}");
		}
	}
}
