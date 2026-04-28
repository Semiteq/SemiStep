using FluentResults;

using SemiStep.Core.Shared;

using Serilog;

namespace SemiStep.Core.Recipes.Import;

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

		string bodyText;
		CsvMetadata metadata;
		try
		{
			(bodyText, metadata) = await CsvFileIo.ReadRecipeFileAsync(filePath);
		}
		catch (IOException ex)
		{
			Log.Warning("IO error while loading recipe from {FilePath}: {Message}", filePath, ex.Message);
			return Result.Fail<Recipe>($"Failed to load recipe from '{filePath}': {ex.Message}");
		}
		catch (UnauthorizedAccessException ex)
		{
			Log.Warning("Access denied while loading recipe from {FilePath}: {Message}", filePath, ex.Message);
			return Result.Fail<Recipe>($"Failed to load recipe from '{filePath}': {ex.Message}");
		}

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

		try
		{
			await CsvFileIo.WriteRecipeFileAsync(csvBody, metadata, filePath);
		}
		catch (IOException ex)
		{
			Log.Warning("IO error while saving recipe to {FilePath}: {Message}", filePath, ex.Message);
			return Result.Fail($"Failed to save recipe to '{filePath}': {ex.Message}");
		}
		catch (UnauthorizedAccessException ex)
		{
			Log.Warning("Access denied while saving recipe to {FilePath}: {Message}", filePath, ex.Message);
			return Result.Fail($"Failed to save recipe to '{filePath}': {ex.Message}");
		}

		Log.Information("Saved recipe to {FilePath}: {StepCount} steps", filePath, recipe.StepCount);

		return Result.Ok();
	}
}
