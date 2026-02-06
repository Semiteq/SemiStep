using Converter;

using Core.Entities;

using Domain.Ports;

using Serilog;

using Shared.Entities;

namespace Domain.Services;

public sealed class PlcSyncService
{
	private const int MaxRetryAttempts = 3;

	private readonly IPlcConnection _connection;
	private readonly RecipeConverter _converter;
	private readonly ILogger _logger;

	public PlcSyncService(
		IPlcConnection connection,
		RecipeConverter converter,
		ILogger logger)
	{
		_connection = connection;
		_converter = converter;
		_logger = logger;
	}

	public async Task<bool> IsRecipeActiveAsync(CancellationToken ct = default)
	{
		var state = await _connection.ReadExecutionStateAsync(ct);
		return state.RecipeActive;
	}

	public async Task SyncRecipeAsync(Recipe recipe, CancellationToken ct = default)
	{
		var recipeData = _converter.FromRecipe(recipe);

		for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
		{
			try
			{
				await _connection.WriteRecipeDataAsync(recipeData, ct);
				_logger.Information(
					"Recipe synced to PLC successfully ({StepCount} steps)",
					recipe.StepCount);
				return;
			}
			catch (PlcSyncException ex) when (attempt < MaxRetryAttempts && IsRetryableError(ex.ErrorCode))
			{
				_logger.Warning(
					"Sync attempt {Attempt} failed with {Error}, retrying...",
					attempt,
					ex.ErrorCode);
			}
		}

		throw new PlcSyncException(
			$"Failed to sync recipe after {MaxRetryAttempts} attempts",
			PlcSyncError.ChecksumMismatchMultiple);
	}

	public async Task<Recipe> LoadRecipeAsync(CancellationToken ct = default)
	{
		var recipeData = await _connection.ReadRecipeDataAsync(ct);
		return _converter.ToRecipe(recipeData);
	}

	private static bool IsRetryableError(PlcSyncError error)
	{
		return error is PlcSyncError.ChecksumMismatchInt
			or PlcSyncError.ChecksumMismatchFloat
			or PlcSyncError.ChecksumMismatchString
			or PlcSyncError.ChecksumMismatchMultiple
			or PlcSyncError.Timeout;
	}
}
