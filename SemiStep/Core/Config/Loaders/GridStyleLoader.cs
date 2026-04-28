using Config.Dto;

using FluentResults;

using TypesShared.Results;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

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
			return Result.Ok<GridStyleOptionsDto?>(null)
				.WithWarning($"UI directory not found, using default grid styles: {uiDir}");
		}

		var filePath = Path.Combine(uiDir, "grid_style.yaml");

		if (!File.Exists(filePath))
		{
			return Result.Ok<GridStyleOptionsDto?>(null)
				.WithWarning($"Grid style file not found, using defaults: {filePath}");
		}

		try
		{
			var content = await File.ReadAllTextAsync(filePath);

			return Result.Ok(_deserializer.Deserialize<GridStyleOptionsDto?>(content));
		}
		catch (Exception ex)
		{
			return Result.Ok<GridStyleOptionsDto?>(null)
				.WithWarning($"Failed to parse grid style file, using defaults: {ex.Message}");
		}
	}
}
