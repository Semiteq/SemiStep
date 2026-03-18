using System.Collections.Immutable;
using System.Globalization;

using Csv.Helpers;

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

	public string Serialize(Recipe recipe)
	{
		var csvColumns = CsvStepWriter.GetCsvColumns(columnRegistry);
		using var stringWriter = new StringWriter();
		using var csvWriter = CreateWriter(stringWriter);

		WriteHeader(csvWriter, csvColumns);

		foreach (var step in recipe.Steps)
		{
			CsvStepWriter.WriteStep(csvWriter, step, csvColumns);
		}

		csvWriter.Flush();

		return stringWriter.ToString();
	}

	public Result<Recipe> Deserialize(string csvBody)
	{
		var csvColumns = CsvStepWriter.GetCsvColumns(columnRegistry);

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

		return new Recipe(steps.ToImmutableList());
	}

	private Result<Step> TryParseStep(
		CsvReader csvReader,
		IReadOnlyList<GridColumnDefinition> columns,
		int rowNumber)
	{
		var rawAction = csvReader.GetField(CsvStepWriter.ActionColumnKey);
		var actionResult = CsvStepReader.TryParseActionValue(rawAction, rowNumber, actionRegistry);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult<Step>();
		}

		var actionKey = actionResult.Value;
		var actionColumnKeys = CsvStepReader.GetActionColumnKeys(actionKey, actionRegistry);
		var properties = CsvStepReader.ParseProperties(
			columns, actionColumnKeys, rowNumber, propertyRegistry,
			column => csvReader.GetField(column.Key));

		if (properties.IsFailed)
		{
			return properties.ToResult<Step>();
		}

		return new Step(actionKey, properties.Value);
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
}
