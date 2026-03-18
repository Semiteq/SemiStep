using System.Collections.Immutable;
using System.Globalization;

using Csv.Helpers;

using CsvHelper;
using CsvHelper.Configuration;

using FluentResults;

using Shared.Config;
using Shared.Config.Contracts;
using Shared.Core;

namespace Csv.ClipboardService;

internal sealed class CsvClipboardSerializer(
	IColumnRegistry columnRegistry,
	IActionRegistry actionRegistry,
	IPropertyRegistry propertyRegistry)
{
	private const char ClipboardSeparator = '\t';

	internal string SerializeSteps(Recipe recipe)
	{
		var csvColumns = CsvStepWriter.GetCsvColumns(columnRegistry);
		using var stringWriter = new StringWriter();
		using var csvWriter = CreateClipboardWriter(stringWriter);

		foreach (var step in recipe.Steps)
		{
			CsvStepWriter.WriteStep(csvWriter, step, csvColumns);
		}

		csvWriter.Flush();

		return stringWriter.ToString();
	}

	internal Result<Recipe> DeserializeSteps(string csvBody)
	{
		try
		{
			var csvColumns = CsvStepWriter.GetCsvColumns(columnRegistry);
			var columnIndexMap = BuildColumnIndexMap(csvColumns);

			var actionColumnIndex = FindActionColumnIndex(csvColumns);
			if (actionColumnIndex < 0)
			{
				return Result.Fail("Action column not found in configuration");
			}

			using var stringReader = new StringReader(csvBody);
			using var csvReader = CreateClipboardReader(stringReader);

			return ReadAllSteps(csvReader, csvColumns, columnIndexMap, actionColumnIndex);
		}
		catch (Exception ex)
		{
			return Result.Fail($"Failed to parse clipboard data: {ex.Message}");
		}
	}

	private Result<Recipe> ReadAllSteps(
		CsvReader csvReader,
		IReadOnlyList<GridColumnDefinition> csvColumns,
		Dictionary<string, int> columnIndexMap,
		int actionColumnIndex)
	{
		var expectedColumnCount = CountColumns_IntendedForUserInput(csvColumns);
		var errors = new List<IError>();
		var steps = new List<Step>();
		var rowNumber = 0;

		while (csvReader.Read())
		{
			rowNumber++;

			var actualColumnCount = csvReader.ColumnCount;
			if (actualColumnCount > expectedColumnCount)
			{
				return Result.Fail(
					$"Column count mismatch on row {rowNumber}: expected {expectedColumnCount}, got {actualColumnCount}. " +
					"The clipboard data does not match the current configuration.");
			}

			var stepResult = TryParseStep(csvReader, csvColumns, columnIndexMap, actionColumnIndex, rowNumber);
			if (stepResult.IsFailed)
			{
				errors.AddRange(stepResult.Errors);
				continue;
			}

			steps.Add(stepResult.Value);
		}

		if (errors.Count > 0)
		{
			return Result.Fail(errors);
		}

		if (steps.Count == 0)
		{
			return Result.Fail("No valid steps found in clipboard data");
		}

		return new Recipe(steps.ToImmutableList());
	}

	private static int CountColumns_IntendedForUserInput(IReadOnlyList<GridColumnDefinition> columns)
	{
		return columns.Count(t => t.SaveToCsv);
	}

	private Result<Step> TryParseStep(
		CsvReader csvReader,
		IReadOnlyList<GridColumnDefinition> csvColumns,
		Dictionary<string, int> columnIndexMap,
		int actionColumnIndex,
		int rowNumber)
	{
		var rawAction = csvReader.GetField(actionColumnIndex);
		var actionResult = CsvStepReader.TryParseActionValue(rawAction, rowNumber, actionRegistry);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult<Step>();
		}

		var actionKey = actionResult.Value;
		var actionColumnKeys = CsvStepReader.GetActionColumnKeys(actionKey, actionRegistry);
		var properties = CsvStepReader.ParseProperties(
			csvColumns, actionColumnKeys, rowNumber, propertyRegistry,
			column => columnIndexMap.TryGetValue(column.Key, out var index)
				? csvReader.GetField(index)
				: null);

		if (properties.IsFailed)
		{
			return properties.ToResult<Step>();
		}

		return new Step(actionKey, properties.Value);
	}

	private static int FindActionColumnIndex(IReadOnlyList<GridColumnDefinition> columns)
	{
		for (var i = 0; i < columns.Count; i++)
		{
			if (columns[i].Key == CsvStepWriter.ActionColumnKey)
			{
				return i;
			}
		}

		return -1;
	}

	private static Dictionary<string, int> BuildColumnIndexMap(IReadOnlyList<GridColumnDefinition> columns)
	{
		var map = new Dictionary<string, int>(columns.Count);

		for (var i = 0; i < columns.Count; i++)
		{
			map[columns[i].Key] = i;
		}

		return map;
	}

	private static CsvWriter CreateClipboardWriter(TextWriter textWriter)
	{
		var config = new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			Delimiter = ClipboardSeparator.ToString(),
			HasHeaderRecord = false,
			TrimOptions = TrimOptions.Trim
		};

		return new CsvWriter(textWriter, config);
	}

	private static CsvReader CreateClipboardReader(TextReader textReader)
	{
		var config = new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			Delimiter = ClipboardSeparator.ToString(),
			HasHeaderRecord = false,
			TrimOptions = TrimOptions.Trim,
			MissingFieldFound = null
		};

		return new CsvReader(textReader, config);
	}
}
