using System.Linq;

using FluentResults;

using S7.Protocol;

using Serilog;

using TypesShared.Core;
using TypesShared.Domain;
using TypesShared.Plc;

namespace S7.Sync;

/// <summary>
/// Owns debounce scheduling and PLC write execution. Called by <see cref="PlcSyncCoordinator"/>.
/// </summary>
internal sealed class PlcSyncExecutor(
	PlcTransactionExecutor transactionExecutor,
	IS7Service connectionService,
	Lock stateLock,
	Action<PlcSyncStatus> setStatus,
	Action<DateTimeOffset> setLastSyncTime)
{
	internal const int DebounceDelayMilliseconds = 1000;

	private Task? _syncTask;
	private CancellationTokenSource? _debounceCts;
	private Recipe? _pendingSnapshot;
	private string? _pendingErrorMessage;
	private volatile bool _disposed;

	public string? PendingErrorMessage
	{
		get
		{
			lock (stateLock)
			{
				return _pendingErrorMessage;
			}
		}
	}

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
				Log.Debug("Sync in progress, queueing new snapshot");

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
			_pendingErrorMessage = null;
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
				Log.Warning(ex, "Sync task did not complete cleanly during disposal");
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
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unhandled exception in sync task");
				lock (stateLock)
				{
					_pendingErrorMessage = ex.Message;
				}
				setStatus(PlcSyncStatus.Failed);
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
		if (!connectionService.IsConnected)
		{
			Log.Debug("Skipping sync: not connected to PLC");
			setStatus(PlcSyncStatus.Disconnected);

			return Result.Fail("Not connected");
		}

		var activeResult = await transactionExecutor.IsRecipeActiveAsync(ct);
		if (activeResult.IsFailed)
		{
			var isDisconnected = activeResult.Errors.OfType<NotConnectedError>().Any();
			lock (stateLock)
			{
				_pendingErrorMessage = isDisconnected
					? "Not connected to PLC"
					: activeResult.Errors[0].Message;
			}
			setStatus(PlcSyncStatus.Failed);

			if (isDisconnected)
			{
				Log.Warning("Sync blocked: not connected to PLC");
			}

			return Result.Fail(activeResult.Errors[0].Message);
		}

		if (activeResult.Value)
		{
			lock (stateLock)
			{
				_pendingErrorMessage = "Recipe is being executed on PLC";
			}
			setStatus(PlcSyncStatus.Failed);
			Log.Warning("Sync blocked: recipe is being executed on PLC");

			return Result.Fail("Recipe active");
		}

		return Result.Ok();
	}

	private async Task WriteSyncAsync(Recipe recipe, CancellationToken ct)
	{
		lock (stateLock)
		{
			_pendingErrorMessage = null;
		}
		setStatus(PlcSyncStatus.Syncing);

		var writeResult = await transactionExecutor.WriteRecipeWithRetryAsync(recipe, ct);
		if (writeResult.IsFailed)
		{
			lock (stateLock)
			{
				_pendingErrorMessage = writeResult.Errors[0].Message;
			}
			setStatus(PlcSyncStatus.Failed);
			if (!writeResult.Errors.OfType<NotConnectedError>().Any())
			{
				Log.Error("Sync failed: {Message}", writeResult.Errors[0].Message);
			}

			return;
		}

		setLastSyncTime(DateTimeOffset.UtcNow);
		lock (stateLock)
		{
			_pendingErrorMessage = null;
		}
		setStatus(PlcSyncStatus.Synced);

		bool hasPending;
		lock (stateLock)
		{
			hasPending = _pendingSnapshot is not null && !_disposed;
		}

		if (hasPending)
		{
			Log.Debug("Changes occurred during sync, starting new debounce");
			lock (stateLock)
			{
				StartDebounce();
			}
		}
	}
}
