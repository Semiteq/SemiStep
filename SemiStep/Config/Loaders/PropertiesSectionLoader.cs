using Config.Dto;
using Config.Models;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

public sealed class PropertiesSectionLoader : ISectionLoader<List<PropertyDto>>
{
	private readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.Build();

	public async Task<List<PropertyDto>> LoadAsync(string configDirectory, ConfigContext context)
	{
		var propertiesDir = Path.Combine(configDirectory, "properties");

		if (!Directory.Exists(propertiesDir))
		{
			context.AddError($"Properties directory not found: {propertiesDir}");
			return [];
		}

		var yamlFiles = Directory.GetFiles(propertiesDir, "*.yaml")
			.OrderBy(f => f)
			.ToList();

		if (yamlFiles.Count == 0)
		{
			context.AddError($"No YAML files found in properties directory: {propertiesDir}");
			return [];
		}

		var allProperties = new List<PropertyDto>();
		var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var file in yamlFiles)
		{
			try
			{
				var content = await File.ReadAllTextAsync(file);
				var properties = _deserializer.Deserialize<List<PropertyDto>>(content);

				if (properties == null)
				{
					context.AddWarning($"Empty or invalid YAML file: {Path.GetFileName(file)}", file);
					continue;
				}

				foreach (var property in properties)
				{
					ValidateProperty(property, file, context, seenIds);
				}

				allProperties.AddRange(properties);
			}
			catch (Exception ex)
			{
				context.AddError($"Failed to parse {Path.GetFileName(file)}: {ex.Message}", file);
			}
		}

		return allProperties;
	}

	private static void ValidateProperty(
		PropertyDto property,
		string file,
		ConfigContext context,
		HashSet<string> seenIds)
	{
		var fileName = Path.GetFileName(file);
		var location = $"{fileName}, PropertyTypeId='{property.PropertyTypeId}'";

		if (string.IsNullOrWhiteSpace(property.PropertyTypeId))
		{
			context.AddError("Property PropertyTypeId is required", fileName);
			return;
		}

		if (!seenIds.Add(property.PropertyTypeId))
		{
			context.AddError($"Duplicate PropertyTypeId: '{property.PropertyTypeId}'", location);
		}

		if (string.IsNullOrWhiteSpace(property.SystemType))
		{
			context.AddError("Property SystemType is required", location);
		}
		else
		{
			var validSystemTypes = new[] { "int", "float", "string" };
			if (!validSystemTypes.Contains(property.SystemType, StringComparer.OrdinalIgnoreCase))
			{
				context.AddError(
					$"Property SystemType must be one of: {string.Join(", ", validSystemTypes)}, got: '{property.SystemType}'",
					location);
			}
		}

		if (string.IsNullOrWhiteSpace(property.FormatKind))
		{
			context.AddError("Property FormatKind is required", location);
		}

		if (property.Min.HasValue && property.Max.HasValue && property.Min > property.Max)
		{
			context.AddError($"Property Min ({property.Min}) cannot be greater than Max ({property.Max})", location);
		}
	}
}
