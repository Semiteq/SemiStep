using Config.Dto;

using FluentResults;

using TypesShared.Results;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

internal static class ColumnsSectionLoader
{
	private static readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

	public static async Task<Result<List<ColumnDto>>> LoadAsync(string configDirectory)
	{
		var columnsDir = Path.Combine(configDirectory, "columns");

		if (!Directory.Exists(columnsDir))
		{
			return Result.Fail($"Columns directory not found: {columnsDir}");
		}

		var yamlFiles = Directory.GetFiles(columnsDir, "*.yaml")
			.OrderBy(f => f)
			.ToList();

		if (yamlFiles.Count == 0)
		{
			return Result.Fail($"No YAML files found in columns directory: {columnsDir}");
		}

		var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var fileResults = new List<Result<List<ColumnDto>>>();

		foreach (var file in yamlFiles)
		{
			fileResults.Add(await LoadFileColumnsAsync(file, seenKeys));
		}

		var merged = Result.Merge(fileResults.ToArray());
		if (merged.IsFailed)
		{
			return merged.ToResult<List<ColumnDto>>();
		}

		var allColumns = fileResults
			.Where(r => r.IsSuccess)
			.SelectMany(r => r.Value)
			.ToList();

		return Result.Ok(allColumns).WithReasons(merged.Reasons);
	}

	private static async Task<Result<List<ColumnDto>>> LoadFileColumnsAsync(
		string filePath,
		HashSet<string> seenKeys)
	{
		try
		{
			var content = await File.ReadAllTextAsync(filePath);
			var columnsDict = _deserializer.Deserialize<Dictionary<string, ColumnDto>>(content);

			if (columnsDict is null || columnsDict.Count == 0)
			{
				return Result.Ok(new List<ColumnDto>())
					.WithWarning($"Empty or invalid YAML file: {Path.GetFileName(filePath)}");
			}

			return ValidateColumnsFromFile(columnsDict, filePath, seenKeys);
		}
		catch (Exception ex)
		{
			return Result.Fail<List<ColumnDto>>(
				$"Failed to parse {Path.GetFileName(filePath)}: {ex.Message}");
		}
	}

	private static Result<List<ColumnDto>> ValidateColumnsFromFile(
		Dictionary<string, ColumnDto> columnsDict,
		string filePath,
		HashSet<string> seenKeys)
	{
		var columns = new List<ColumnDto>();
		var validationResults = new List<Result>();

		foreach (var (key, columnContent) in columnsDict)
		{
			var column = new ColumnDto
			{
				Key = key,
				ColumnType = columnContent.ColumnType,
				Ui = columnContent.Ui,
				BusinessLogic = columnContent.BusinessLogic
			};

			var validationResult = ValidateColumn(column, filePath, seenKeys);
			validationResults.Add(validationResult);

			if (validationResult.IsSuccess)
			{
				columns.Add(column);
			}
		}

		var merged = Result.Merge(validationResults.ToArray());
		if (merged.IsFailed)
		{
			return merged.ToResult<List<ColumnDto>>();
		}

		return Result.Ok(columns).WithReasons(merged.Reasons);
	}

	private static Result ValidateColumn(
		ColumnDto column,
		string file,
		HashSet<string> seenKeys)
	{
		var fileName = Path.GetFileName(file);
		var location = $"{fileName}, Key='{column.Key}'";

		if (string.IsNullOrWhiteSpace(column.Key))
		{
			return Result.Fail($"[{fileName}] Column Key is required");
		}

		var validationResults = new List<Result>();

		if (!seenKeys.Add(column.Key))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Duplicate column Key: '{column.Key}'"));
		}

		if (string.IsNullOrWhiteSpace(column.ColumnType))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column ColumnType is required"));
		}

		if (column.Ui == null)
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column Ui section is required"));
		}
		else if (string.IsNullOrWhiteSpace(column.Ui.UiName))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column Ui.UiName is required"));
		}

		if (column.BusinessLogic == null)
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column BusinessLogic section is required"));
		}
		else if (string.IsNullOrWhiteSpace(column.BusinessLogic.PropertyTypeId))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column BusinessLogic.PropertyTypeId is required"));
		}

		if (validationResults.Count == 0)
		{
			return Result.Ok();
		}

		return Result.Merge(validationResults.ToArray());
	}
}
