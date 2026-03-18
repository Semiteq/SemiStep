using Csv.FsService;
using Csv.Helpers;

using FluentResults;

using Serilog;

using Shared.Core;
using Shared.Results;
using Shared.ServiceContracts;

namespace Csv.Facade;

internal sealed class CsvService(CsvFileSerializer csvFileSerializer) : ICsvService
{
	public async Task<Result<Recipe>> LoadAsync(string filePath, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(filePath))
		{
			return Result.Fail<Recipe>($"Recipe file not found: {filePath}");
		}

		var (bodyText, metadata) = await CsvFileIo.ReadRecipeFileAsync(filePath, cancellationToken);
		var result = csvFileSerializer.Deserialize(bodyText);

		if (result.IsFailed)
		{
			return result;
		}

		var warnings = new List<string>();

		if (metadata.Rows > 0 && metadata.Rows != result.Value.StepCount)
		{
			warnings.Add(
				$"Row count mismatch in '{filePath}': metadata says {metadata.Rows}, actual is {result.Value.StepCount}");
		}

		Log.Information("Loaded recipe from {FilePath}: {StepCount} steps", filePath, result.Value.StepCount);

		return Result.Ok(result.Value).WithWarnings(warnings);
	}

	public async Task SaveAsync(Recipe recipe, string filePath, CancellationToken cancellationToken = default)
	{
		var csvBody = csvFileSerializer.Serialize(recipe);
		var metadata = CsvFileIo.BuildSaveMetadata(csvBody);

		await CsvFileIo.WriteRecipeFileAsync(csvBody, metadata, filePath, cancellationToken);

		Log.Information("Saved recipe to {FilePath}: {StepCount} steps", filePath, recipe.StepCount);
	}
}
