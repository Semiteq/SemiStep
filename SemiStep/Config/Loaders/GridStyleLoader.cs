using Config.Dto;
using Config.Models;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

public sealed class GridStyleLoader : ISectionLoader<GridStyleOptionsDto?>
{
	private readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.Build();

	public async Task<GridStyleOptionsDto?> LoadAsync(string configDirectory, ConfigContext context)
	{
		var uiDir = Path.Combine(configDirectory, "ui");

		if (!Directory.Exists(uiDir))
		{
			context.AddInfo($"UI directory not found, using default grid styles: {uiDir}");
			return null;
		}

		var filePath = Path.Combine(uiDir, "grid_style.yaml");

		if (!File.Exists(filePath))
		{
			context.AddInfo($"Grid style file not found, using defaults: {filePath}");
			return null;
		}

		try
		{
			var content = await File.ReadAllTextAsync(filePath);
			var dto = _deserializer.Deserialize<GridStyleOptionsDto>(content);

			if (dto == null)
			{
				context.AddWarning("Grid style file is empty, using defaults", filePath);
				return null;
			}

			return dto;
		}
		catch (Exception ex)
		{
			context.AddWarning($"Failed to parse grid style file, using defaults: {ex.Message}", filePath);
			return null;
		}
	}
}
