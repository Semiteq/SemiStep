using CsvHelper;

using TypesShared.Config;
using TypesShared.Core;

namespace Csv;

internal static class CsvStepWriter
{
	internal const string ActionColumnKey = StepValueParser.ActionColumnKey;

	internal static IReadOnlyList<GridColumnDefinition> GetCsvColumns(ConfigRegistry configRegistry)
	{
		return configRegistry.GetAllColumns()
			.Where(c => c.SaveToCsv)
			.ToList();
	}

	internal static void WriteStep(CsvWriter csvWriter, Step step, IReadOnlyList<GridColumnDefinition> columns)
	{
		foreach (var column in columns)
		{
			csvWriter.WriteField(StepValueParser.FormatStepValue(step, column));
		}

		csvWriter.NextRecord();
	}
}
