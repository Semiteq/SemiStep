using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.Sync;

/// <summary>
/// Owns debounce scheduling and PLC write execution. Called by <see cref="PlcSyncCoordinator"/>.
/// </summary>
internal sealed class PlcSyncExecutor(
	PlcTransactionExecutor transactionExecutor,
	IS7Connection connection,
	Lock stateLock,
	Action<PlcSyncStatus> setStatus,
	Action<DateTimeOffset> setLastSyncTime,
	Action<IError> reportFault,
	ILogger<PlcSyncExecutor> logger)
{
	internal const int DebounceDelayMilliseconds = 1000;

	private readonly ILogger<PlcSyncExecutor> _logger = logger;
	private Task? _syncTask;
	private CancellationTokenSource? _debounceCts;
	private Recipe? _pendingSnapshot;
	private volatile bool _disposed;

	public void OnRecipeChanged(Recipe recipe)
	{
		lock (stateLock)
		{
			if (_disposed)
			{
				return;
			}

			_pendingSnapshot = recipe;

			if (_syncTask is not null && !_syncTask.IsCompleted)
			{
				_logger.LogDebug("Sync in progress, queueing new snapshot");

				return;
			}

			StartDebounce();
		}
	}

	public void ClearPendingSnapshot()
	{
		lock (stateLock)
		{
			_pendingSnapshot = null;
		}
	}

	public void Reset()
	{
		lock (stateLock)
		{
			_debounceCts?.Cancel();
			_debounceCts?.Dispose();
			_debounceCts = null;
			_pendingSnapshot = null;
		}
	}

	public async Task WaitForPendingSyncAsync(CancellationToken ct)
	{
		Task? taskToWait;
		lock (stateLock)
		{
			taskToWait = _syncTask;
		}

		if (taskToWait is not null)
		{
			try
			{
				await taskToWait.WaitAsync(ct);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				throw;
			}
			catch (OperationCanceledException)
			{
				// Internal debounce cancellation — not a caller cancellation.
			}
		}
	}

	public void Dispose()
	{
		Task? taskToWait;
		lock (stateLock)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_debounceCts?.Cancel();
			_debounceCts?.Dispose();
			_debounceCts = null;
			taskToWait = _syncTask;
			_syncTask = null;
		}

		if (taskToWait is not null)
		{
			try
			{
				taskToWait.Wait(TimeSpan.FromSeconds(5));
			}
			catch (AggregateException ex) when (ex.Flatten().InnerExceptions.All(e => e is OperationCanceledException))
			{
				// Expected on cancellation — ignore.
			}
			catch (AggregateException ex)
			{
				_logger.LogWarning(ex, "Sync task did not complete cleanly during disposal");
			}
		}
	}

	private void StartDebounce()
	{
		_debounceCts?.Cancel();
		_debounceCts?.Dispose();
		_debounceCts = new CancellationTokenSource();

		var ct = _debounceCts.Token;
		_syncTask = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(DebounceDelayMilliseconds, ct);
				await ExecuteSyncAsync(ct);
			}
			catch (OperationCanceledException)
			{
				// Expected: the debounce window was preempted by a newer sync request
				// that cancelled this token. The newer request scheduled its own task.
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unhandled exception in sync task");
				setStatus(PlcSyncStatus.Failed);
				reportFault(new Error(ex.Message));
			}
		}, ct);
	}

	private async Task ExecuteSyncAsync(CancellationToken ct)
	{
		var snapshotToSync = ConsumePendingSnapshot();

		if (snapshotToSync is null)
		{
			return;
		}

		var canSyncResult = await CheckCanSyncAsync(ct);
		if (canSyncResult.IsFailed)
		{
			return;
		}

		await WriteSyncAsync(snapshotToSync, ct);
	}

	private Recipe? ConsumePendingSnapshot()
	{
		lock (stateLock)
		{
			var snapshot = _pendingSnapshot;
			_pendingSnapshot = null;
			return snapshot;
		}
	}

	private async Task<Result> CheckCanSyncAsync(CancellationToken ct)
	{
		if (!connection.IsConnected)
		{
			_logger.LogDebug("Skipping sync: not connected to PLC");

			return Result.Fail("Not connected");
		}

		var activeResult = await transactionExecutor.IsRecipeActiveAsync(ct);
		if (activeResult.IsFailed)
		{
			var isDisconnected = activeResult.Errors.OfType<NotConnectedError>().Any();
			var faultMessage = isDisconnected
				? "Not connected to PLC"
				: activeResult.Errors[0].Message;
			setStatus(PlcSyncStatus.Failed);
			reportFault(new Error(faultMessage));

			if (isDisconnected)
			{
				_logger.LogWarning("Sync blocked: not connected to PLC");
			}

			return Result.Fail(activeResult.Errors[0].Message);
		}

		if (activeResult.Value)
		{
			setStatus(PlcSyncStatus.Failed);
			reportFault(new Error("Recipe is being executed on PLC"));
			_logger.LogWarning("Sync blocked: recipe is being executed on PLC");

			return Result.Fail("Recipe active");
		}

		return Result.Ok();
	}

	private async Task WriteSyncAsync(Recipe recipe, CancellationToken ct)
	{
		setStatus(PlcSyncStatus.Syncing);

		var writeResult = await transactionExecutor.WriteRecipeWithRetryAsync(recipe, ct);
		if (writeResult.IsFailed)
		{
			setStatus(PlcSyncStatus.Failed);
			reportFault(new Error(writeResult.Errors[0].Message));
			if (!writeResult.Errors.OfType<NotConnectedError>().Any())
			{
				_logger.LogError("Sync failed: {Message}", writeResult.Errors[0].Message);
			}

			return;
		}

		setLastSyncTime(DateTimeOffset.UtcNow);
		setStatus(PlcSyncStatus.Synced);

		bool hasPending;
		lock (stateLock)
		{
			hasPending = _pendingSnapshot is not null && !_disposed;
		}

		if (hasPending)
		{
			_logger.LogDebug("Changes occurred during sync, starting new debounce");
			lock (stateLock)
			{
				StartDebounce();
			}
		}
	}
}
