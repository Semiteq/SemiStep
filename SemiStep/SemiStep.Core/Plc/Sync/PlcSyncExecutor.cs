using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.Sync;

/// <summary>
/// The two stateless steps of one sync attempt, a pre-check and a write.
/// <see cref="PlcSyncCoordinator"/> owns the state.
/// </summary>
internal sealed class PlcSyncExecutor(
	PlcTransactionExecutor transactionExecutor,
	IS7Connection connection,
	ILogger<PlcSyncExecutor> logger)
{
	private readonly ILogger<PlcSyncExecutor> _logger = logger;

	public async Task<SyncPrecheckOutcome> CheckCanSyncAsync(CancellationToken ct)
	{
		if (!connection.IsConnected)
		{
			_logger.LogDebug("Skipping sync: not connected to PLC");

			return new SyncPrecheckOutcome.NotConnected();
		}

		var activeResult = await transactionExecutor.IsRecipeActiveAsync(ct);
		if (activeResult.IsFailed)
		{
			if (activeResult.Errors.OfType<NotConnectedError>().Any())
			{
				_logger.LogWarning("Sync blocked: not connected to PLC");
			}

			return new SyncPrecheckOutcome.Failed(activeResult.Errors[0]);
		}

		if (activeResult.Value)
		{
			_logger.LogWarning("Sync blocked: recipe is being executed on PLC");

			return new SyncPrecheckOutcome.Failed(new RecipeActiveError());
		}

		return new SyncPrecheckOutcome.Ready();
	}

	public async Task<Result> WriteAsync(Recipe recipe, CancellationToken ct)
	{
		var writeResult = await transactionExecutor.WriteRecipeWithRetryAsync(recipe, ct);
		if (writeResult.IsFailed
			&& !writeResult.Errors.OfType<NotConnectedError>().Any()
			&& !writeResult.Errors.OfType<PlcCommandFailedError>().Any())
		{
			_logger.LogError("Sync failed: {Message}", writeResult.Errors[0].Message);
		}

		return writeResult;
	}
}
