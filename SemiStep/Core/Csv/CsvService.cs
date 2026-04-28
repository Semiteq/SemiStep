using FluentResults;

using Serilog;

using TypesShared.Core;
using TypesShared.Results;

namespace Csv;

public class CsvService
{
	private readonly CsvFileSerializer _csvFileSerializer;

	internal CsvService(CsvFileSerializer csvFileSerializer)
	{
		_csvFileSerializer = csvFileSerializer;
	}

	public virtual async Task<Result<Recipe>> LoadAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return Result.Fail<Recipe>($"Recipe file not found: {filePath}");
		}

		var (bodyText, metadata) = await CsvFileIo.ReadRecipeFileAsync(filePath);
		var result = _csvFileSerializer.Deserialize(bodyText);

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

	public virtual async Task<Result> SaveAsync(Recipe recipe, string filePath)
	{
		var csvBody = _csvFileSerializer.Serialize(recipe);
		var metadata = CsvFileIo.BuildSaveMetadata(csvBody);

		await CsvFileIo.WriteRecipeFileAsync(csvBody, metadata, filePath);

		Log.Information("Saved recipe to {FilePath}: {StepCount} steps", filePath, recipe.StepCount);

		return Result.Ok();
	}
}
