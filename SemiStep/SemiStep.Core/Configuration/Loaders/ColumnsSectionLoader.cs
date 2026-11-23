using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Loaders;

internal static class ColumnsSectionLoader
{
	public static Task<Result<List<ColumnDto>>> LoadAsync(string configDirectory)
	{
		return MergeSectionLoader.LoadAsync<string, ColumnDto, ColumnDto>(
			configDirectory,
			ConfigFolderPolicy.ColumnsFolder,
			StringComparer.OrdinalIgnoreCase,
			MapColumn);
	}

	private static Result<ColumnDto> MapColumn(string key, ColumnDto columnContent, string fileName)
	{
		var column = new ColumnDto
		{
			Key = key,
			ColumnType = columnContent.ColumnType,
			Ui = columnContent.Ui,
			BusinessLogic = columnContent.BusinessLogic
		};

		return ValidateColumn(column, fileName).ToResult(column);
	}

	private static Result ValidateColumn(
		ColumnDto column,
		string fileName)
	{
		var location = $"{fileName}, Key='{column.Key}'";

		if (string.IsNullOrWhiteSpace(column.Key))
		{
			return Result.Fail($"[{fileName}] Column Key is required");
		}

		var validationResults = new List<Result>();

		if (string.IsNullOrWhiteSpace(column.ColumnType))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column ColumnType is required"));
		}

		if (column.Ui == null)
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column Ui section is required"));
		}
		else if (string.IsNullOrWhiteSpace(column.Ui.UiName))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column Ui.UiName is required"));
		}

		if (column.BusinessLogic == null)
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column BusinessLogic section is required"));
		}
		else if (string.IsNullOrWhiteSpace(column.BusinessLogic.PropertyTypeId))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Column BusinessLogic.PropertyTypeId is required"));
		}

		return validationResults.Count == 0 ? Result.Ok() : Result.Merge([.. validationResults]);
	}
}
