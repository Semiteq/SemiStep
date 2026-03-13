using System.Collections.Immutable;
using System.Globalization;

using CsvHelper;
using CsvHelper.Configuration;

using FluentResults;

using Shared.Config;
using Shared.Config.Contracts;
using Shared.Core;

namespace Csv.FsService;

internal sealed class CsvFileSerializer(
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

	public Result<Recipe> Deserialize(string csvBody)
	{
		var csvColumns = GetCsvColumns();

		using var stringReader = new StringReader(csvBody);
		using var csvReader = CreateReader(stringReader);

		csvReader.Read();
		csvReader.ReadHeader();

		var headerResult = ValidateHeader(csvReader, csvColumns);
		if (headerResult.IsFailed)
		{
			var errorList = headerResult.Errors.Select(e => e.Message).ToList();
			return Result.Fail<Recipe>(errorList);
		}

		var allErrors = new List<IError>();
		var steps = new List<Step>();
		var rowNumber = 1;

		while (csvReader.Read())
		{
			rowNumber++;
			var stepResult = TryParseStep(csvReader, csvColumns, rowNumber);

			if (stepResult.IsFailed)
			{
				allErrors.AddRange(stepResult.Errors);
			}
			else
			{
				steps.Add(stepResult.Value);
			}
		}

		if (allErrors.Count > 0)
		{
			return Result.Fail<Recipe>(allErrors);
		}

		var recipe = new Recipe(steps.ToImmutableList());

		return Result.Ok(recipe);
	}

	private Result<Step> TryParseStep(
		CsvReader csvReader,
		IReadOnlyList<GridColumnDefinition> columns,
		int rowNumber)
	{
		var actionKeyResult = TryParseActionKey(csvReader, rowNumber)
			.Bind(ValidateActionKey);

		if (actionKeyResult.IsFailed)
		{
			return actionKeyResult.ToResult<Step>();
		}

		var actionKey = actionKeyResult.Value;
		var actionColumnKeys = GetActionColumnKeys(actionKey);
		var properties = ParseProperties(csvReader, columns, actionColumnKeys, rowNumber);

		if (properties.IsFailed)
		{
			return properties.ToResult<Step>();
		}

		return Result.Ok(new Step(actionKey, properties.Value));
	}

	private Result<ImmutableDictionary<ColumnId, PropertyValue>> ParseProperties(
		CsvReader csvReader,
		IReadOnlyList<GridColumnDefinition> columns,
		HashSet<string> actionColumnKeys,
		int rowNumber)
	{

		var errors = new List<IError>();
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
			var propertyResult = StepValueParser.TryParsePropertyValue(rawValue, propertyDef, column.Key, rowNumber);
			if (propertyResult.IsFailed)
			{
				errors.AddRange(propertyResult.Errors);
			}
			else
			{
				properties.Add(new ColumnId(column.Key), propertyResult.Value);
			}
		}

		if (errors.Count > 0)
		{
			return Result.Fail(errors);
		}

		return properties.ToImmutable();
	}

	private HashSet<string> GetActionColumnKeys(int actionKey)
	{
		var action = actionRegistry.GetAction(actionKey);

		return action.Columns
			.Select(c => c.Key)
			.ToHashSet();
	}

	private Result<int> ValidateActionKey(int actionKey)
	{
		if (!actionRegistry.ActionExists(actionKey))
		{
			return Result.Fail($"Unknown action ID '{actionKey}'");
		}

		return actionKey;
	}

	private static Result<int> TryParseActionKey(CsvReader csvReader, int rowNumber)
	{
		var rawAction = csvReader.GetField(ActionColumnKey);

		if (string.IsNullOrWhiteSpace(rawAction))
		{
			return Result.Fail($"Row {rowNumber}: action column is empty");
		}

		if (!int.TryParse(rawAction, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actionKey))
		{
			return Result.Fail($"Row {rowNumber}: cannot parse action value '{rawAction}' as integer");
		}

		return actionKey;
	}

	private static Result ValidateHeader(
		CsvReader csvReader,
		IReadOnlyList<GridColumnDefinition> expectedColumns)
	{
		var actualHeader = csvReader.HeaderRecord;
		var expected = expectedColumns.Select(c => c.Key).ToArray();

		if (actualHeader is null || actualHeader.Length == 0)
		{
			return Result.Fail(
				$"CSV header mismatch. Expected: [{string.Join("; ", expected)}], Actual: []");
		}

		var trimmedActual = actualHeader.Select(h => h.Trim()).ToArray();

		if (!expected.SequenceEqual(trimmedActual))
		{
			return Result.Fail(
				$"CSV header mismatch. Expected: [{string.Join("; ", expected)}], " +
				$"Actual: [{string.Join("; ", trimmedActual)}]");
		}

		return Result.Ok();
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

	private static void WriteSteps(CsvWriter csvWriter, Recipe recipe, IReadOnlyList<GridColumnDefinition> columns)
	{
		foreach (var step in recipe.Steps)
		{
			WriteStep(csvWriter, step, columns);
		}
	}

	private static void WriteStep(CsvWriter csvWriter, Step step, IReadOnlyList<GridColumnDefinition> columns)
	{
		foreach (var column in columns)
		{
			var value = StepValueParser.FormatStepValue(step, column);
			csvWriter.WriteField(value);
		}

		csvWriter.NextRecord();
	}
}
