using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Recipes.Import.Errors;
using SemiStep.Core.Recipes.Import.Warnings;
using SemiStep.Core.Shared;

namespace SemiStep.Core.Recipes.Import;

public class CsvService
{
	private readonly CsvFileSerializer _csvFileSerializer;
	private readonly ILogger<CsvService> _logger;

	public CsvService(CsvFileSerializer csvFileSerializer, ILogger<CsvService> logger)
	{
		_csvFileSerializer = csvFileSerializer;
		_logger = logger;
	}

	public virtual async Task<Result<Recipe>> LoadAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return Result.Fail(new RecipeFileNotFoundError(filePath));
		}

		string bodyText;
		CsvMetadata metadata;
		try
		{
			(bodyText, metadata) = await CsvFileIo.ReadRecipeFileAsync(filePath);
		}
		catch (IOException ex)
		{
			_logger.LogWarning("IO error while loading recipe from {FilePath}: {Message}", filePath, ex.Message);
			return Result.Fail(new RecipeLoadFailedError(filePath).CausedBy(ex));
		}
		catch (UnauthorizedAccessException ex)
		{
			_logger.LogWarning("Access denied while loading recipe from {FilePath}: {Message}", filePath, ex.Message);
			return Result.Fail(new RecipeLoadFailedError(filePath).CausedBy(ex));
		}

		var result = _csvFileSerializer.Deserialize(bodyText);

		if (result.IsFailed)
		{
			return result;
		}

		var okResult = Result.Ok(result.Value);

		if (metadata.Rows > 0 && metadata.Rows != result.Value.StepCount)
		{
			okResult = okResult.WithSuccess(
				new RowCountMismatchWarning(filePath, metadata.Rows, result.Value.StepCount));
		}

		_logger.LogInformation("Loaded recipe from {FilePath}: {StepCount} steps", filePath, result.Value.StepCount);

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
			_logger.LogWarning("IO error while saving recipe to {FilePath}: {Message}", filePath, ex.Message);
			return Result.Fail(new RecipeSaveFailedError(filePath).CausedBy(ex));
		}
		catch (UnauthorizedAccessException ex)
		{
			_logger.LogWarning("Access denied while saving recipe to {FilePath}: {Message}", filePath, ex.Message);
			return Result.Fail(new RecipeSaveFailedError(filePath).CausedBy(ex));
		}

		_logger.LogInformation("Saved recipe to {FilePath}: {StepCount} steps", filePath, recipe.StepCount);

		return Result.Ok();
	}
}
