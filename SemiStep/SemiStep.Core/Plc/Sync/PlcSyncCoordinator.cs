using System.Reactive.Subjects;

using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.Sync;

/// <summary>
/// The sync state machine: status, debounce, pending recipe and in-flight attempt under one state lock.
/// </summary>
internal sealed class PlcSyncCoordinator(
	PlcTransactionExecutor transactionExecutor,
	IS7Connection connection,
	ILoggerFactory loggerFactory) : IPlcSyncService, IDisposable
{
	private const int DebounceDelayMilliseconds = 1000;

	private static readonly TimeSpan _disposeWaitTimeout = TimeSpan.FromSeconds(5);

	// Lock order: _emissionGate, then _stateLock.
	// OnNext runs under the gate only, so state getters never wait on a subscriber.
	private readonly Lock _emissionGate = new();
	private readonly Lock _stateLock = new();
	private readonly BehaviorSubject<PlcSessionSnapshot> _subject = new(
		PlcSessionSnapshot.InitialState);
	private readonly Subject<IError> _faults = new();
	private readonly PlcSyncExecutor _executor = new(
		transactionExecutor,
		connection,
		loggerFactory.CreateLogger<PlcSyncExecutor>());
	private readonly ILogger<PlcSyncCoordinator> _logger = loggerFactory.CreateLogger<PlcSyncCoordinator>();

	// Guarded by _stateLock; written from the UI caller, the sync pool task and the S7 connection callback.
	private PlcConnectionState _connectionState = PlcConnectionState.Disconnected;
	private bool _isSyncEnabled;
	private PlcSyncStatus _status = PlcSyncStatus.Idle;
	private Recipe? _pendingRecipe;
	private CancellationTokenSource? _debounceCts;
	private CancellationTokenSource? _runningAttempt;
	private Task? _syncTask;
	private bool _disposed;

	public bool IsSyncEnabled
	{
		get
		{
			lock (_stateLock)
			{
				return _isSyncEnabled;
			}
		}
	}

	public PlcSyncStatus Status
	{
		get
		{
			lock (_stateLock)
			{
				return _status;
			}
		}
	}

	public DateTimeOffset? LastSyncTime
	{
		get
		{
			lock (_stateLock)
			{
				return field;
			}
		}

		private set;
	}

	public IObservable<PlcSessionSnapshot> PlcState => _subject;

	public IObservable<IError> Faults => _faults;

	public void NotifyRecipeChanged(Recipe recipe, bool isValid)
	{
		if (!isValid)
		{
			Transition(() =>
			{
				_pendingRecipe = null;
				return new TransitionResult(SetStatusLocked(PlcSyncStatus.OutOfSync), null, null);
			});
			return;
		}

		StartDebounceWhen(() =>
		{
			_pendingRecipe = recipe;
			if (_runningAttempt is null)
			{
				return true;
			}

			_logger.LogDebug("Sync in progress, queueing new recipe");
			return false;
		});
	}

	public void SetSyncEnabled(bool value)
	{
		Transition(() =>
		{
			_isSyncEnabled = value;
			return new TransitionResult(SnapshotLocked(), null, null);
		});
	}

	public void UpdateConnectionState(PlcConnectionState state)
	{
		Transition(() =>
		{
			_connectionState = state;
			return new TransitionResult(SnapshotLocked(), null, null);
		});
	}

	public void ResetForDisable()
	{
		Transition(() => new TransitionResult(SetStatusLocked(PlcSyncStatus.Idle), null, CancelAttemptLocked()));
	}

	public void HandleConnectionLost()
	{
		Transition(() =>
		{
			var snapshot = _status == PlcSyncStatus.Syncing ? SetStatusLocked(PlcSyncStatus.Failed) : null;
			return new TransitionResult(snapshot, new ConnectionLostError(), CancelAttemptLocked());
		});
	}

	public async Task WaitForPendingSyncAsync(CancellationToken ct = default)
	{
		Task? taskToWait;
		lock (_stateLock)
		{
			taskToWait = _syncTask;
		}

		if (taskToWait is null)
		{
			return;
		}

		try
		{
			await taskToWait.WaitAsync(ct);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
		}
	}

	public void Dispose()
	{
		CancellationTokenSource? debounceCts;
		Task? taskToWait;
		lock (_stateLock)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			debounceCts = _debounceCts;
			taskToWait = _syncTask;
			_syncTask = null;
		}

		debounceCts?.Cancel();
		WaitForSyncTaskOnDispose(taskToWait);
		debounceCts?.Dispose();

		lock (_emissionGate)
		{
			_subject.OnCompleted();
			_subject.Dispose();
			_faults.OnCompleted();
			_faults.Dispose();
		}
	}

	private void WaitForSyncTaskOnDispose(Task? taskToWait)
	{
		if (taskToWait is null)
		{
			return;
		}

		try
		{
			taskToWait.Wait(_disposeWaitTimeout);
		}
		catch (AggregateException ex) when (ex.Flatten().InnerExceptions.All(e => e is OperationCanceledException))
		{
		}
		catch (AggregateException ex)
		{
			_logger.LogWarning(ex, "Sync task did not complete cleanly during disposal");
		}
	}

	private void StartDebounceWhen(Func<bool> shouldStartLocked)
	{
		CancellationTokenSource? retired;
		lock (_stateLock)
		{
			if (_disposed || !shouldStartLocked())
			{
				return;
			}

			retired = StartDebounceLocked();
		}

		Retire(retired);
	}

	private CancellationTokenSource? StartDebounceLocked()
	{
		var retired = _debounceCts;
		var attempt = new CancellationTokenSource();
		var ct = attempt.Token;
		_debounceCts = attempt;
		_runningAttempt = attempt;
		_syncTask = Task.Run(() => RunAttemptAsync(attempt, ct));

		return retired;
	}

	private CancellationTokenSource? CancelAttemptLocked()
	{
		var retired = _debounceCts;
		_debounceCts = null;
		_pendingRecipe = null;

		return retired;
	}

	private async Task RunAttemptAsync(CancellationTokenSource attempt, CancellationToken ct)
	{
		try
		{
			await Task.Delay(DebounceDelayMilliseconds, ct);
			await SyncPendingRecipeAsync(attempt, ct);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unhandled exception in sync task");
			ApplyFailure(attempt, new PlcCommandFailedError().CausedBy(ex));
		}
		finally
		{
			EndAttempt(attempt);
		}
	}

	private async Task SyncPendingRecipeAsync(CancellationTokenSource attempt, CancellationToken ct)
	{
		var recipeToSync = ConsumePendingRecipe(attempt);
		if (recipeToSync is null)
		{
			return;
		}

		var precheck = await _executor.CheckCanSyncAsync(ct);
		if (precheck is SyncPrecheckOutcome.Failed failed)
		{
			ApplyFailure(attempt, failed.Error);
			return;
		}

		if (precheck is not SyncPrecheckOutcome.Ready)
		{
			return;
		}

		var isSyncing = ApplyOutcome(
			attempt,
			() => new TransitionResult(SetStatusLocked(PlcSyncStatus.Syncing), null, null));
		if (!isSyncing)
		{
			return;
		}

		var writeResult = await _executor.WriteAsync(recipeToSync, ct);
		if (writeResult.IsFailed)
		{
			ApplyFailure(attempt, writeResult.Errors[0]);
			return;
		}

		ApplyOutcome(attempt, CompleteWriteLocked);
	}

	private Recipe? ConsumePendingRecipe(CancellationTokenSource attempt)
	{
		lock (_stateLock)
		{
			if (_disposed || !ReferenceEquals(_debounceCts, attempt))
			{
				return null;
			}

			var recipe = _pendingRecipe;
			_pendingRecipe = null;
			return recipe;
		}
	}

	private TransitionResult CompleteWriteLocked()
	{
		LastSyncTime = DateTimeOffset.UtcNow;
		var snapshot = SetStatusLocked(PlcSyncStatus.Synced);
		if (_pendingRecipe is null)
		{
			_runningAttempt = null;
			return new TransitionResult(snapshot, null, null);
		}

		_logger.LogDebug("Changes occurred during sync, starting new debounce");
		return new TransitionResult(snapshot, null, StartDebounceLocked());
	}

	private void ApplyFailure(CancellationTokenSource attempt, IError fault)
	{
		ApplyOutcome(attempt, () =>
		{
			_runningAttempt = null;
			return new TransitionResult(SetStatusLocked(PlcSyncStatus.Failed), fault, null);
		});
	}

	private bool ApplyOutcome(CancellationTokenSource attempt, Func<TransitionResult> applyLocked)
	{
		var applied = false;
		Transition(() =>
		{
			if (!ReferenceEquals(_debounceCts, attempt))
			{
				return default;
			}

			applied = true;
			return applyLocked();
		});

		return applied;
	}

	private void EndAttempt(CancellationTokenSource attempt)
	{
		StartDebounceWhen(() =>
		{
			if (!ReferenceEquals(_runningAttempt, attempt))
			{
				return false;
			}

			_runningAttempt = null;
			return _pendingRecipe is not null;
		});
	}

	private void Transition(Func<TransitionResult> mutateLocked)
	{
		var result = default(TransitionResult);
		try
		{
			lock (_emissionGate)
			{
				lock (_stateLock)
				{
					if (_disposed)
					{
						return;
					}

					result = mutateLocked();
				}

				Emit(result);
			}
		}
		finally
		{
			Retire(result.Retired);
		}
	}

	private void Emit(TransitionResult result)
	{
		if (result.Snapshot is { } snapshot)
		{
			_subject.OnNext(snapshot);
		}

		if (result.Fault is { } fault)
		{
			_faults.OnNext(fault);
		}
	}

	private PlcSessionSnapshot? SetStatusLocked(PlcSyncStatus status)
	{
		if (_status == status)
		{
			return null;
		}

		_status = status;
		return SnapshotLocked();
	}

	private PlcSessionSnapshot SnapshotLocked()
	{
		return new PlcSessionSnapshot(_connectionState, _status, _isSyncEnabled);
	}

	private static void Retire(CancellationTokenSource? retired)
	{
		retired?.Cancel();
		retired?.Dispose();
	}

	private readonly record struct TransitionResult(
		PlcSessionSnapshot? Snapshot,
		IError? Fault,
		CancellationTokenSource? Retired);
}
