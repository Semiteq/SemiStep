using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Loaders;

internal static class GridStyleLoader
{
	public static async Task<Result<GridStyleOptionsDto?>> LoadAsync(string configDirectory)
	{
		var uiDir = Path.Combine(configDirectory, ConfigFolderPolicy.UiFolder);

		if (!Directory.Exists(uiDir))
		{
			return Result.Fail<GridStyleOptionsDto?>(new GridStyleConfigNotFoundError(uiDir));
		}

		var filePath = Path.Combine(uiDir, ConfigFolderPolicy.GridStyleFileName);

		if (!File.Exists(filePath))
		{
			return Result.Fail<GridStyleOptionsDto?>(new GridStyleConfigNotFoundError(filePath));
		}

		try
		{
			var content = await File.ReadAllTextAsync(filePath);

			return Result.Ok(YamlConfigDeserializer.Instance.Deserialize<GridStyleOptionsDto?>(content));
		}
		catch (Exception ex)
		{
			return Result.Fail(new GridStyleLoadFailedError(Path.GetFileName(filePath)).CausedBy(ex));
		}
	}
}
