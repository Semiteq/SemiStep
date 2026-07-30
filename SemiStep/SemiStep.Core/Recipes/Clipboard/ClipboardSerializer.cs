using System.Collections.Immutable;
using System.Globalization;

using CsvHelper;
using CsvHelper.Configuration;

using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Recipes.Clipboard.Errors;
using SemiStep.Core.Recipes.Errors;
using SemiStep.Core.Recipes.Import.Errors;

namespace SemiStep.Core.Recipes.Clipboard;

public sealed class ClipboardSerializer(
	RecipeMetadataRegistry recipeMetadataRegistry,
	ILogger<ClipboardSerializer> logger)
{
	private const char Separator = '\t';

	public string SerializeSteps(Recipe recipe)
	{
		var csvColumns = GetClipboardColumns();
		using var stringWriter = new StringWriter();
		using var csvWriter = CreateWriter(stringWriter);

		foreach (var step in recipe.Steps)
		{
			foreach (var column in csvColumns)
			{
				csvWriter.WriteField(StepValueParser.FormatStepValue(step, column));
			}

			csvWriter.NextRecord();
		}

		csvWriter.Flush();

		return stringWriter.ToString();
	}

	public Result<Recipe> DeserializeSteps(string tsvBody)
	{
		try
		{
			var clipboardReadonlyColumns = GetClipboardColumns();
			var columnIndexMap = BuildColumnIndexMap(clipboardReadonlyColumns);
			var actionColumnIndexResult = FindActionColumnIndexOrFail(clipboardReadonlyColumns);

			if (actionColumnIndexResult.IsFailed)
			{
				return actionColumnIndexResult.ToResult();
			}

			var actionColumnIndex = actionColumnIndexResult.Value;
			using var stringReader = new StringReader(tsvBody);
			using var csvReader = CreateReader(stringReader);

			return ReadAllSteps(csvReader, clipboardReadonlyColumns, columnIndexMap, actionColumnIndex);
		}
		catch (Exception ex)
		{
			logger.LogWarning("Failed to parse clipboard data: {Message}", ex.Message);
			return Result.Fail(new ClipboardParseFailedError().CausedBy(ex));
		}
	}

	private List<GridColumnDefinition> GetClipboardColumns()
	{
		return recipeMetadataRegistry
			.GetAllColumns()
			.Where(c => c.SaveToCsv)
			.ToList();
	}

	private Result<Recipe> ReadAllSteps(
		CsvReader csvReader,
		List<GridColumnDefinition> csvColumns,
		Dictionary<string, int> columnIndexMap,
		int actionColumnIndex)
	{
		var errors = new List<IError>();
		var steps = new List<Step>();
		var rowNumber = 0;

		while (csvReader.Read())
		{
			rowNumber++;

			if (csvReader.ColumnCount > csvColumns.Count)
			{
				return Result.Fail(
					new ColumnCountMismatchError(rowNumber, csvColumns.Count, csvReader.ColumnCount));
			}

			var stepResult = TryParseStep(csvReader, csvColumns, columnIndexMap, actionColumnIndex);
			if (stepResult.IsFailed)
			{
				foreach (var error in stepResult.Errors)
				{
					errors.Add(new AtRowError(rowNumber, error));
				}

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
			return Result.Fail(new NoValidStepsError());
		}

		return new Recipe(steps.ToImmutableList());
	}

	private Result<Step> TryParseStep(
		CsvReader csvReader,
		List<GridColumnDefinition> csvColumns,
		Dictionary<string, int> columnIndexMap,
		int actionColumnIndex)
	{
		var rawAction = csvReader.GetField(actionColumnIndex);
		if (string.IsNullOrWhiteSpace(rawAction))
		{
			return Result.Fail(new ActionColumnEmptyError());
		}

		if (!int.TryParse(rawAction, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actionKey))
		{
			return Result.Fail(new ActionValueNotIntegerError(rawAction));
		}

		if (recipeMetadataRegistry.ActionExists(actionKey).IsFailed)
		{
			return Result.Fail(new ActionByIdNotFoundError(actionKey));
		}

		var actionDef = recipeMetadataRegistry.GetAction(actionKey).Value;
		var actionPropertyKeys = actionDef.Properties
			.Select(p => p.Key)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		return ParseProperties(csvReader, csvColumns, columnIndexMap, actionPropertyKeys, actionKey);
	}

	private Result<Step> ParseProperties(
		CsvReader csvReader,
		List<GridColumnDefinition> csvColumns,
		Dictionary<string, int> columnIndexMap,
		HashSet<string> actionPropertyKeys,
		int actionKey)
	{
		var errors = new List<IError>();
		var properties = ImmutableDictionary.CreateBuilder<PropertyId, PropertyValue>();

		foreach (var column in csvColumns)
		{
			if (column.Key == StepValueParser.ActionColumnKey)
			{
				continue;
			}

			if (!actionPropertyKeys.Contains(column.Key))
			{
				continue;
			}

			if (!columnIndexMap.TryGetValue(column.Key, out var fieldIndex))
			{
				continue;
			}

			var rawValue = csvReader.GetField(fieldIndex);
			if (string.IsNullOrWhiteSpace(rawValue))
			{
				continue;
			}

			if (recipeMetadataRegistry.PropertyExists(column.PropertyTypeId).IsFailed)
			{
				continue;
			}

			var propertyTypeDef = recipeMetadataRegistry.GetProperty(column.PropertyTypeId).Value;
			var parseResult = PropertyParser.Parse(rawValue, propertyTypeDef);
			if (parseResult.IsFailed)
			{
				foreach (var error in parseResult.Errors)
				{
					errors.Add(new AtColumnError(column.Key, error));
				}
			}
			else
			{
				properties.Add(new PropertyId(column.Key), parseResult.Value);
			}
		}

		if (errors.Count > 0)
		{
			return Result.Fail(errors);
		}

		return new Step(actionKey, properties.ToImmutable());
	}

	private static Result<int> FindActionColumnIndexOrFail(List<GridColumnDefinition> columns)
	{
		for (var i = 0; i < columns.Count; i++)
		{
			if (columns[i].Key == StepValueParser.ActionColumnKey)
			{
				return i;
			}
		}

		return Result.Fail(new ActionColumnNotFoundError());
	}

	private static Dictionary<string, int> BuildColumnIndexMap(List<GridColumnDefinition> columns)
	{
		var map = new Dictionary<string, int>(columns.Count);

		for (var i = 0; i < columns.Count; i++)
		{
			map[columns[i].Key] = i;
		}

		return map;
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
			HasHeaderRecord = false,
			TrimOptions = TrimOptions.Trim,
			MissingFieldFound = null,
		};

		return new CsvReader(textReader, config);
	}
}
