using FluentResults;

using Serilog;

using TypesShared.Core;
using TypesShared.Domain;
using TypesShared.Results;

namespace Csv;

internal sealed class CsvService(CsvFileSerializer csvFileSerializer) : ICsvService
{
	public async Task<Result<Recipe>> LoadAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return Result.Fail<Recipe>($"Recipe file not found: {filePath}");
		}

		var (bodyText, metadata) = await CsvFileIo.ReadRecipeFileAsync(filePath);
		var result = csvFileSerializer.Deserialize(bodyText);

		if (result.IsFailed)
		{
			return result;
		}

		var okResult = Result.Ok(result.Value);

		if (metadata.Rows > 0 && metadata.Rows != result.Value.StepCount)
		{
			okResult = okResult.WithWarning(
				$"Row count mismatch in '{filePath}': metadata says {metadata.Rows}, actual is {result.Value.StepCount}");
		}

		Log.Information("Loaded recipe from {FilePath}: {StepCount} steps", filePath, result.Value.StepCount);

		return okResult;
	}

	public async Task SaveAsync(Recipe recipe, string filePath)
	{
		var csvBody = csvFileSerializer.Serialize(recipe);
		var metadata = CsvFileIo.BuildSaveMetadata(csvBody);

		await CsvFileIo.WriteRecipeFileAsync(csvBody, metadata, filePath);

		Log.Information("Saved recipe to {FilePath}: {StepCount} steps", filePath, recipe.StepCount);
	}
}
