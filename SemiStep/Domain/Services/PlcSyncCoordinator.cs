using Core.Entities;

using Domain.Ports;
using Domain.State;

using Serilog;

namespace Domain.Services;

public enum SyncStatus
{
	Idle,
	Syncing,
	Synced,
	Failed,
	BlockedByExecution
}

public sealed class PlcSyncCoordinator : IDisposable
{
	private const int DebounceDelayMs = 1000;

	private readonly PlcSyncService _syncService;
	private readonly PlcConnectionManager _connectionManager;
	private readonly RecipeStateManager _stateManager;
	private readonly ILogger _logger;
	private readonly object _lock = new();

	private CancellationTokenSource? _debounceCts;
	private Task? _syncTask;
	private Recipe? _pendingSnapshot;
	private SyncStatus _status = SyncStatus.Idle;
	private string? _lastError;
	private bool _disposed;

	public PlcSyncCoordinator(
		PlcSyncService syncService,
		PlcConnectionManager connectionManager,
		RecipeStateManager stateManager,
		ILogger logger)
	{
		_syncService = syncService;
		_connectionManager = connectionManager;
		_stateManager = stateManager;
		_logger = logger;

		_stateManager.RecipeChanged += OnRecipeChanged;
	}

	public event Action<SyncStatus>? StatusChanged;
	public event Action<string?>? ErrorChanged;

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
				_logger.Debug("Sync in progress, queueing new snapshot");
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

		if (!_connectionManager.IsConnected)
		{
			_logger.Debug("Skipping sync: not connected to PLC");
			return;
		}

		try
		{
			var isRecipeActive = await _syncService.IsRecipeActiveAsync(ct);
			if (isRecipeActive)
			{
				Status = SyncStatus.BlockedByExecution;
				LastError = "Recipe is being executed on PLC";
				_logger.Warning("Sync blocked: recipe is being executed on PLC");
				return;
			}

			Status = SyncStatus.Syncing;
			LastError = null;

			await _syncService.SyncRecipeAsync(snapshotToSync, ct);

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
			_logger.Error(ex, "Sync failed: {Message}", ex.Message);
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
			_logger.Error(ex, "Unexpected error during sync");
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
				_logger.Debug("Changes occurred during sync, starting new debounce");
				StartDebounce(nextSnapshot);
			}
		}
	}
}
