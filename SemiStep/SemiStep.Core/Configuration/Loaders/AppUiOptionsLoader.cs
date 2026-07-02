using FluentResults;

using SemiStep.Core.Configuration.Dto;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemiStep.Core.Configuration.Loaders;

internal static class AppUiOptionsLoader
{
	private static readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

	public static async Task<Result<AppUiOptionsDto?>> LoadAsync(string configDirectory)
	{
		var filePath = Path.Combine(configDirectory, "ui", "app.yaml");

		// Optional file: absence yields defaults, unlike the required grid_style.yaml.
		if (!File.Exists(filePath))
		{
			return Result.Ok<AppUiOptionsDto?>(null);
		}

		try
		{
			var content = await File.ReadAllTextAsync(filePath);

			return Result.Ok(_deserializer.Deserialize<AppUiOptionsDto?>(content));
		}
		catch (Exception ex)
		{
			return Result.Fail($"Failed to load {Path.GetFileName(filePath)}: {ex.Message}");
		}
	}
}
