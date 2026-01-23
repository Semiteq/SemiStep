using Config.Dto;
using Config.Models;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

public sealed class ColumnsSectionLoader : ISectionLoader<List<ColumnDto>>
{
	private readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.Build();

	public async Task<List<ColumnDto>> LoadAsync(string configDirectory, ConfigContext context)
	{
		var columnsDir = Path.Combine(configDirectory, "columns");

		if (!Directory.Exists(columnsDir))
		{
			context.AddError($"Columns directory not found: {columnsDir}");
			return [];
		}

		var yamlFiles = Directory.GetFiles(columnsDir, "*.yaml")
			.OrderBy(f => f)
			.ToList();

		if (yamlFiles.Count == 0)
		{
			context.AddError($"No YAML files found in columns directory: {columnsDir}");
			return [];
		}

		var allColumns = new List<ColumnDto>();
		var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var file in yamlFiles)
		{
			try
			{
				var content = await File.ReadAllTextAsync(file);
				var columns = _deserializer.Deserialize<List<ColumnDto>>(content);

				if (columns == null)
				{
					context.AddWarning($"Empty or invalid YAML file: {Path.GetFileName(file)}", file);
					continue;
				}

				foreach (var column in columns)
				{
					ValidateColumn(column, file, context, seenKeys);
				}

				allColumns.AddRange(columns);
			}
			catch (Exception ex)
			{
				context.AddError($"Failed to parse {Path.GetFileName(file)}: {ex.Message}", file);
			}
		}

		return allColumns;
	}

	private static void ValidateColumn(ColumnDto column, string file, ConfigContext context, HashSet<string> seenKeys)
	{
		var fileName = Path.GetFileName(file);
		var location = $"{fileName}, Key='{column.Key}'";

		if (string.IsNullOrWhiteSpace(column.Key))
		{
			context.AddError("Column Key is required", fileName);
			return;
		}

		if (!seenKeys.Add(column.Key))
		{
			context.AddError($"Duplicate column Key: '{column.Key}'", location);
		}

		if (string.IsNullOrWhiteSpace(column.ColumnType))
		{
			context.AddError("Column ColumnType is required", location);
		}

		if (column.Ui == null)
		{
			context.AddError("Column Ui section is required", location);
		}
		else if (string.IsNullOrWhiteSpace(column.Ui.UiName))
		{
			context.AddError("Column Ui.UiName is required", location);
		}

		if (column.BusinessLogic == null)
		{
			context.AddError("Column BusinessLogic section is required", location);
		}
		else if (string.IsNullOrWhiteSpace(column.BusinessLogic.PropertyTypeId))
		{
			context.AddError("Column BusinessLogic.PropertyTypeId is required", location);
		}
	}
}
