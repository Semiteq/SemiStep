using FluentResults;

using SemiStep.Core.Configuration.Dto;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemiStep.Core.Configuration.Loaders;

internal static class GridStyleLoader
{
	private static readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

	public static async Task<Result<GridStyleOptionsDto?>> LoadAsync(string configDirectory)
	{
		var uiDir = Path.Combine(configDirectory, "ui");

		if (!Directory.Exists(uiDir))
		{
			return Result.Fail<GridStyleOptionsDto?>(new GridStyleConfigNotFoundError(uiDir));
		}

		var filePath = Path.Combine(uiDir, "grid_style.yaml");

		if (!File.Exists(filePath))
		{
			return Result.Fail<GridStyleOptionsDto?>(new GridStyleConfigNotFoundError(filePath));
		}

		try
		{
			var content = await File.ReadAllTextAsync(filePath);

			return Result.Ok(_deserializer.Deserialize<GridStyleOptionsDto?>(content));
		}
		catch (Exception ex)
		{
			return Result.Fail(new GridStyleLoadFailedError(Path.GetFileName(filePath)).CausedBy(ex));
		}
	}
}
