using Core.ConfigImport.Dto;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

/// <summary>
/// Загрузчик конфигурации из YAML файлов
/// </summary>
public sealed class YamlFileLoader : IFileLoader
{
	private readonly IDeserializer _deserializer;

	public YamlFileLoader()
	{
		// Настройка YamlDotNet десериализатора
		_deserializer = new DeserializerBuilder()
			.WithNamingConvention(CamelCaseNamingConvention.Instance)
			.Build();
	}

	/// <inheritdoc />
	public async Task<ConfigRootDto> LoadAsync(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
			throw new ArgumentException("File path cannot be empty", nameof(filePath));

		if (!File.Exists(filePath))
			throw new FileNotFoundException($"Configuration file not found: {filePath}", filePath);

		// TODO: Реализовать загрузку и десериализацию YAML
		// 1. Прочитать файл
		var yamlContent = await File.ReadAllTextAsync(filePath);

		// 2. Десериализовать в ConfigRootDto
		var config = _deserializer.Deserialize<ConfigRootDto>(yamlContent);

		if (config == null)
			throw new InvalidOperationException($"Failed to deserialize configuration from {filePath}");

		return config;
	}

	/// <inheritdoc />
	public bool SupportsFormat(string filePath)
	{
		var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
		return extension == ".yaml" || extension == ".yml";
	}
}
