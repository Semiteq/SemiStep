using FluentResults;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Shared;

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

		// Grid styles are cosmetic — both a missing ui directory and a missing grid_style.yaml
		// are legitimate; defaults apply in either case.
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
			return Result.Fail($"Failed to load {Path.GetFileName(filePath)}: {ex.Message}");
		}
	}
}
