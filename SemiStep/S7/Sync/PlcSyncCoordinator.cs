using Core.Entities;

using Domain.Ports;
using Domain.State;

using S7.Protocol;

using Serilog;

namespace S7.Sync;

public sealed class PlcSyncCoordinator : IDisposable
{
	private const int DebounceDelayMs = 1000;
	private readonly IS7ConnectionService _connectionService;
	private readonly Lock _lock = new();
	private readonly RecipeStateManager _stateManager;
	private readonly PlcTransactionExecutor _transactionExecutor;

	private CancellationTokenSource? _debounceCts;
	private bool _disposed;
	private string? _lastError;
	private Recipe? _pendingSnapshot;
	private SyncStatus _status = SyncStatus.Idle;
	private Task? _syncTask;

	public PlcSyncCoordinator(
		PlcTransactionExecutor transactionExecutor,
		IS7ConnectionService connectionService,
		RecipeStateManager stateManager)
	{
		_transactionExecutor = transactionExecutor;
		_connectionService = connectionService;
		_stateManager = stateManager;

		_stateManager.RecipeChanged += OnRecipeChanged;
	}

	public SyncStatus Status
	{
		get
		{
			lock (_lock)
			{
				return _status;
			}
		}
		private set
		{
			lock (_lock)
			{
				if (_status == value)
				{
					return;
				}
				_status = value;
			}
			StatusChanged?.Invoke(value);
		}
	}

	public string? LastError
	{
		get
		{
			lock (_lock)
			{
				return _lastError;
			}
		}
		private set
		{
			lock (_lock)
			{
				_lastError = value;
			}
			ErrorChanged?.Invoke(value);
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_stateManager.RecipeChanged -= OnRecipeChanged;
		_debounceCts?.Cancel();
		_debounceCts?.Dispose();
	}

	public event Action<SyncStatus>? StatusChanged;
	public event Action<string?>? ErrorChanged;

	public async Task WaitForPendingSyncAsync(CancellationToken ct = default)
	{
		Task? taskToWait;
		lock (_lock)
		{
			taskToWait = _syncTask;
		}

		if (taskToWait is not null)
		{
			try
			{
				await taskToWait.WaitAsync(ct);
			}
			catch (OperationCanceledException)
			{
			}
		}
	}

	private void OnRecipeChanged(Recipe recipe)
	{
		if (_disposed)
		{
			return;
		}

		lock (_lock)
		{
			_pendingSnapshot = recipe;

			if (_syncTask is not null && !_syncTask.IsCompleted)
			{
				Log.Debug("Sync in progress, queueing new snapshot");

				return;
			}

			StartDebounce(recipe);
		}
	}

	private void StartDebounce(Recipe recipe)
	{
		_debounceCts?.Cancel();
		_debounceCts?.Dispose();
		_debounceCts = new CancellationTokenSource();

		var ct = _debounceCts.Token;
		_syncTask = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(DebounceDelayMs, ct);
				await ExecuteSyncAsync(ct);
			}
			catch (OperationCanceledException)
			{
			}
		}, ct);
	}

	private async Task ExecuteSyncAsync(CancellationToken ct)
	{
		Recipe? snapshotToSync;
		lock (_lock)
		{
			snapshotToSync = _pendingSnapshot;
			_pendingSnapshot = null;
		}

		if (snapshotToSync is null)
		{
			return;
		}

		if (!_connectionService.IsConnected)
		{
			Log.Debug("Skipping sync: not connected to PLC");

			return;
		}

		try
		{
			var isRecipeActive = await _transactionExecutor.IsRecipeActiveAsync(ct);
			if (isRecipeActive)
			{
				Status = SyncStatus.BlockedByExecution;
				LastError = "Recipe is being executed on PLC";
				Log.Warning("Sync blocked: recipe is being executed on PLC");

				return;
			}

			Status = SyncStatus.Syncing;
			LastError = null;

			await _transactionExecutor.WriteRecipeWithRetryAsync(snapshotToSync, ct);

			Status = SyncStatus.Synced;
			LastError = null;
		}
		catch (PlcRecipeActiveException)
		{
			Status = SyncStatus.BlockedByExecution;
			LastError = "Recipe is being executed on PLC";
		}
		catch (PlcSyncException ex)
		{
			Status = SyncStatus.Failed;
			LastError = ex.Message;
			Log.Error(ex, "Sync failed: {Message}", ex.Message);
		}
		catch (PlcNotConnectedException)
		{
			Status = SyncStatus.Failed;
			LastError = "Not connected to PLC";
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Status = SyncStatus.Failed;
			LastError = ex.Message;
			Log.Error(ex, "Unexpected error during sync");
		}
		finally
		{
			Recipe? nextSnapshot;
			lock (_lock)
			{
				nextSnapshot = _pendingSnapshot;
			}

			if (nextSnapshot is not null)
			{
				Log.Debug("Changes occurred during sync, starting new debounce");
				StartDebounce(nextSnapshot);
			}
		}
	}
}
