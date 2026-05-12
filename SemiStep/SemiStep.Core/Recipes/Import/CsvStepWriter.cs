using CsvHelper;

using SemiStep.Core.Configuration;

namespace SemiStep.Core.Recipes.Import;

internal static class CsvStepWriter
{
	internal const string ActionColumnKey = StepValueParser.ActionColumnKey;

	internal static IReadOnlyList<GridColumnDefinition> GetCsvColumns(RecipeMetadataRegistry recipeMetadataRegistry)
	{
		return recipeMetadataRegistry.GetAllColumns()
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
