using System.Collections.Immutable;
using System.Globalization;

using CsvHelper;
using CsvHelper.Configuration;

using FluentResults;

using TypesShared.Config;
using TypesShared.Core;

namespace Csv;

internal sealed class CsvFileSerializer(
	ConfigRegistry configRegistry,
	CsvRowConverter converter)
{
	private const char Separator = ';';

	public string Serialize(Recipe recipe)
	{
		var csvColumns = CsvStepWriter.GetCsvColumns(configRegistry);
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
		var lines = csvBody.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		if (lines.Length == 0)
		{
			return Result.Fail<Recipe>("CSV body is empty");
		}

		var headerResult = ValidateHeader(lines[0]);
		if (headerResult.IsFailed)
		{
			return headerResult.ToResult<Recipe>();
		}

		var allErrors = new List<IError>();
		var steps = new List<Step>();

		for (var i = 1; i < lines.Length; i++)
		{
			var stepResult = converter.Convert(lines[i]);
			if (stepResult.IsFailed)
			{
				var rowNumber = i + 1;
				foreach (var error in stepResult.Errors)
				{
					allErrors.Add(new Error($"Row {rowNumber}").CausedBy(error));
				}
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

		return Result.Ok(new Recipe(steps.ToImmutableList()));
	}

	private Result ValidateHeader(string headerLine)
	{
		var actual = headerLine.Split(Separator, StringSplitOptions.TrimEntries);
		var expected = converter.ColumnOrder;

		if (!expected.SequenceEqual(actual))
		{
			return Result.Fail(
				$"CSV header mismatch. Expected: [{string.Join("; ", expected)}], " +
				$"Actual: [{string.Join("; ", actual)}]");
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

	private static void WriteHeader(CsvWriter csvWriter, IReadOnlyList<GridColumnDefinition> columns)
	{
		foreach (var column in columns)
		{
			csvWriter.WriteField(column.Key);
		}

		csvWriter.NextRecord();
	}
}
