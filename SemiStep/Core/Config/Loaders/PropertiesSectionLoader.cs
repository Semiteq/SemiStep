using Config.Dto;

using FluentResults;

using TypesShared.Results;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

internal static class PropertiesSectionLoader
{
	private static readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

	public static async Task<Result<List<PropertyDto>>> LoadAsync(string configDirectory)
	{
		var propertiesDir = Path.Combine(configDirectory, "properties");

		if (!Directory.Exists(propertiesDir))
		{
			return Result.Fail($"Properties directory not found: {propertiesDir}");
		}

		var yamlFiles = Directory.GetFiles(propertiesDir, "*.yaml")
			.OrderBy(f => f)
			.ToList();

		if (yamlFiles.Count == 0)
		{
			return Result.Fail($"No YAML files found in properties directory: {propertiesDir}");
		}

		var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var fileResults = new List<Result<List<PropertyDto>>>();

		foreach (var file in yamlFiles)
		{
			fileResults.Add(await LoadFilePropertiesAsync(file, seenIds));
		}

		var merged = Result.Merge(fileResults.ToArray());
		if (merged.IsFailed)
		{
			return merged.ToResult<List<PropertyDto>>();
		}

		var allProperties = fileResults
			.Where(r => r.IsSuccess)
			.SelectMany(r => r.Value)
			.ToList();

		return Result.Ok(allProperties).WithReasons(merged.Reasons);
	}

	private static async Task<Result<List<PropertyDto>>> LoadFilePropertiesAsync(
		string filePath,
		HashSet<string> seenIds)
	{
		try
		{
			var content = await File.ReadAllTextAsync(filePath);
			var propertiesDict = _deserializer.Deserialize<Dictionary<string, PropertyDto>>(content);

			if (propertiesDict is null || propertiesDict.Count == 0)
			{
				return Result.Ok(new List<PropertyDto>())
					.WithWarning($"Empty or invalid YAML file: {Path.GetFileName(filePath)}");
			}

			return ValidatePropertiesFromFile(propertiesDict, filePath, seenIds);
		}
		catch (Exception ex)
		{
			return Result.Fail<List<PropertyDto>>(
				$"Failed to parse {Path.GetFileName(filePath)}: {ex.Message}");
		}
	}

	private static Result<List<PropertyDto>> ValidatePropertiesFromFile(
		Dictionary<string, PropertyDto> propertiesDict,
		string filePath,
		HashSet<string> seenIds)
	{
		var properties = new List<PropertyDto>();
		var validationResults = new List<Result>();

		foreach (var (propertyTypeId, propertyContent) in propertiesDict)
		{
			var property = new PropertyDto
			{
				PropertyTypeId = propertyTypeId,
				SystemType = propertyContent.SystemType,
				FormatKind = propertyContent.FormatKind,
				Units = propertyContent.Units,
				Min = propertyContent.Min,
				Max = propertyContent.Max,
				MaxLength = propertyContent.MaxLength
			};

			var validationResult = ValidateProperty(property, filePath, seenIds);
			validationResults.Add(validationResult);

			if (validationResult.IsSuccess)
			{
				properties.Add(property);
			}
		}

		var merged = Result.Merge(validationResults.ToArray());
		if (merged.IsFailed)
		{
			return merged.ToResult<List<PropertyDto>>();
		}

		return Result.Ok(properties).WithReasons(merged.Reasons);
	}

	private static Result ValidateProperty(
		PropertyDto property,
		string file,
		HashSet<string> seenIds)
	{
		var fileName = Path.GetFileName(file);
		var location = $"{fileName}, PropertyTypeId='{property.PropertyTypeId}'";

		if (string.IsNullOrWhiteSpace(property.PropertyTypeId))
		{
			return Result.Fail($"[{fileName}] Property PropertyTypeId is required");
		}

		var validationResults = new List<Result>();

		if (!seenIds.Add(property.PropertyTypeId))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Duplicate PropertyTypeId: '{property.PropertyTypeId}'"));
		}

		if (string.IsNullOrWhiteSpace(property.SystemType))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Property SystemType is required"));
		}
		else
		{
			var validSystemTypes = new[] { "int", "float", "string" };
			if (!validSystemTypes.Contains(property.SystemType, StringComparer.OrdinalIgnoreCase))
			{
				validationResults.Add(Result.Fail(
					$"[{location}] Property SystemType must be one of: {string.Join(", ", validSystemTypes)}, got: '{property.SystemType}'"));
			}
		}

		if (string.IsNullOrWhiteSpace(property.FormatKind))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Property FormatKind is required"));
		}

		if (property.Min.HasValue && property.Max.HasValue && property.Min > property.Max)
		{
			validationResults.Add(Result.Fail(
				$"[{location}] Property Min ({property.Min}) cannot be greater than Max ({property.Max})"));
		}

		if (validationResults.Count == 0)
		{
			return Result.Ok();
		}

		return Result.Merge(validationResults.ToArray());
	}
}
