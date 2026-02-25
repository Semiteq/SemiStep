using System.Collections.Immutable;
using System.Globalization;

using Core.Entities;

using CsvHelper;
using CsvHelper.Configuration;

using Shared.Entities;
using Shared.Registries;

namespace Csv;

public sealed class CsvSerializer(
	IColumnRegistry columnRegistry,
	IActionRegistry actionRegistry,
	IPropertyRegistry propertyRegistry)
{
	private const char Separator = ';';
	private const string ActionColumnKey = "action";

	public string Serialize(Recipe recipe)
	{
		var csvColumns = GetCsvColumns();
		using var stringWriter = new StringWriter();
		using var csvWriter = CreateWriter(stringWriter);

		WriteHeader(csvWriter, csvColumns);
		WriteSteps(csvWriter, recipe, csvColumns);

		csvWriter.Flush();

		return stringWriter.ToString();
	}

	public Recipe Deserialize(string csvBody)
	{
		var csvColumns = GetCsvColumns();

		using var stringReader = new StringReader(csvBody);
		using var csvReader = CreateReader(stringReader);

		csvReader.Read();
		csvReader.ReadHeader();
		ValidateHeader(csvReader, csvColumns);

		var steps = new List<Step>();
		var rowNumber = 1;

		while (csvReader.Read())
		{
			rowNumber++;
			var step = ParseStep(csvReader, csvColumns, rowNumber);
			steps.Add(step);
		}

		return new Recipe(steps.ToImmutableList());
	}

	private IReadOnlyList<GridColumnDefinition> GetCsvColumns()
	{
		return columnRegistry.GetAll()
			.Where(c => c.SaveToCsv)
			.ToList();
	}

	private static CsvWriter CreateWriter(TextWriter textWriter)
	{
		var config = new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			Delimiter = Separator.ToString(),
			HasHeaderRecord = false,
			TrimOptions = TrimOptions.Trim,
		};

		return new CsvWriter(textWriter, config);
	}

	private static CsvReader CreateReader(TextReader textReader)
	{
		var config = new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			Delimiter = Separator.ToString(),
			HasHeaderRecord = true,
			TrimOptions = TrimOptions.Trim,
			MissingFieldFound = null,
		};

		return new CsvReader(textReader, config);
	}

	private static void WriteHeader(CsvWriter csvWriter, IReadOnlyList<GridColumnDefinition> columns)
	{
		foreach (var column in columns)
		{
			csvWriter.WriteField(column.Key);
		}

		csvWriter.NextRecord();
	}

	private void WriteSteps(CsvWriter csvWriter, Recipe recipe, IReadOnlyList<GridColumnDefinition> columns)
	{
		foreach (var step in recipe.Steps)
		{
			WriteStep(csvWriter, step, columns);
		}
	}

	private void WriteStep(CsvWriter csvWriter, Step step, IReadOnlyList<GridColumnDefinition> columns)
	{
		foreach (var column in columns)
		{
			var value = FormatStepValue(step, column);
			csvWriter.WriteField(value);
		}

		csvWriter.NextRecord();
	}

	private string FormatStepValue(Step step, GridColumnDefinition column)
	{
		if (column.Key == ActionColumnKey)
		{
			return step.ActionKey.ToString(CultureInfo.InvariantCulture);
		}

		var columnId = new ColumnId(column.Key);
		if (!step.Properties.TryGetValue(columnId, out var propertyValue))
		{
			return string.Empty;
		}

		return propertyValue.Type switch
		{
			PropertyType.Int => propertyValue.AsInt().ToString(CultureInfo.InvariantCulture),
			PropertyType.Float => propertyValue.AsFloat().ToString("R", CultureInfo.InvariantCulture),
			PropertyType.String => propertyValue.AsString(),
			_ => propertyValue.Value?.ToString() ?? string.Empty
		};
	}

	private static void ValidateHeader(CsvReader csvReader, IReadOnlyList<GridColumnDefinition> expectedColumns)
	{
		var actualHeader = csvReader.HeaderRecord;
		if (actualHeader is null || actualHeader.Length == 0)
		{
			throw new CsvHeaderMismatchException(
				expectedColumns.Select(c => c.Key).ToArray(),
				[]);
		}

		var expectedKeys = expectedColumns.Select(c => c.Key).ToArray();
		var trimmedActual = actualHeader.Select(h => h.Trim()).ToArray();

		if (!expectedKeys.SequenceEqual(trimmedActual))
		{
			throw new CsvHeaderMismatchException(expectedKeys, trimmedActual);
		}
	}

	private Step ParseStep(CsvReader csvReader, IReadOnlyList<GridColumnDefinition> columns, int rowNumber)
	{
		var actionKey = ParseActionKey(csvReader, rowNumber);

		if (!actionRegistry.ActionExists(actionKey))
		{
			throw new CsvParseException($"Unknown action ID '{actionKey}'", rowNumber);
		}

		var action = actionRegistry.GetAction(actionKey);
		var actionColumnKeys = action.Columns
			.Select(c => c.Key)
			.ToHashSet();

		var properties = ImmutableDictionary.CreateBuilder<ColumnId, PropertyValue>();

		foreach (var column in columns)
		{
			if (column.Key == ActionColumnKey)
			{
				continue;
			}

			var rawValue = csvReader.GetField(column.Key);
			if (string.IsNullOrWhiteSpace(rawValue))
			{
				continue;
			}

			if (!actionColumnKeys.Contains(column.Key))
			{
				continue;
			}

			var propertyDef = propertyRegistry.GetProperty(column.PropertyTypeId);
			var propertyValue = ParsePropertyValue(rawValue, propertyDef, column.Key, rowNumber);
			properties.Add(new ColumnId(column.Key), propertyValue);
		}

		return new Step(actionKey, properties.ToImmutable());
	}

	private static int ParseActionKey(CsvReader csvReader, int rowNumber)
	{
		var rawAction = csvReader.GetField(ActionColumnKey);
		if (string.IsNullOrWhiteSpace(rawAction))
		{
			throw new CsvParseException("Action column is empty", rowNumber);
		}

		if (!int.TryParse(rawAction, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actionKey))
		{
			throw new CsvParseException($"Cannot parse action value '{rawAction}' as integer", rowNumber);
		}

		return actionKey;
	}

	private static PropertyValue ParsePropertyValue(
		string rawValue,
		PropertyDefinition propertyDef,
		string columnKey,
		int rowNumber)
	{
		return propertyDef.SystemType.ToLowerInvariant() switch
		{
			"int" => ParseIntProperty(rawValue, columnKey, rowNumber),
			"float" => ParseFloatProperty(rawValue, columnKey, rowNumber),
			"string" => new PropertyValue(rawValue, PropertyType.String),
			_ => throw new CsvParseException(
				$"Unknown system type '{propertyDef.SystemType}' for column '{columnKey}'", rowNumber)
		};
	}

	private static PropertyValue ParseIntProperty(string rawValue, string columnKey, int rowNumber)
	{
		if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
		{
			throw new CsvParseException(
				$"Cannot parse value '{rawValue}' as int for column '{columnKey}'", rowNumber);
		}

		return new PropertyValue(intValue, PropertyType.Int);
	}

	private static PropertyValue ParseFloatProperty(string rawValue, string columnKey, int rowNumber)
	{
		if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
		{
			throw new CsvParseException(
				$"Cannot parse value '{rawValue}' as float for column '{columnKey}'", rowNumber);
		}

		return new PropertyValue(floatValue, PropertyType.Float);
	}
}
