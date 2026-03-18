using Csv.FsService;

using CsvHelper;

using Shared.Config;
using Shared.Config.Contracts;
using Shared.Core;

namespace Csv.Helpers;

internal static class CsvStepWriter
{
	internal const string ActionColumnKey = "action";

	internal static IReadOnlyList<GridColumnDefinition> GetCsvColumns(IColumnRegistry columnRegistry)
	{
		return columnRegistry.GetAll()
			.Where(c => c.SaveToCsv)
			.ToList();
	}

	internal static void WriteStep(CsvWriter csvWriter, Step step, IReadOnlyList<GridColumnDefinition> columns)
	{
		foreach (var column in columns)
		{
			var value = StepValueParser.FormatStepValue(step, column);
			csvWriter.WriteField(value);
		}

		csvWriter.NextRecord();
	}
}
