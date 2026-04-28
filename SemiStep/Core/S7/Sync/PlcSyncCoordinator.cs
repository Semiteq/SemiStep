using System.Reactive.Subjects;

using FluentResults;

using S7.Protocol;

using TypesShared.Core;
using TypesShared.Domain;
using TypesShared.Plc;

namespace S7.Sync;

internal sealed class PlcSyncCoordinator : IPlcSyncService, IDisposable
{
	private readonly Lock _lock = new();
	private readonly BehaviorSubject<Result<PlcSessionSnapshot>> _subject = new(
		PlcSessionSnapshot.InitialState);
	private readonly PlcSyncExecutor _executor;

	private PlcConnectionState _connectionState = PlcConnectionState.Disconnected;
	private volatile bool _disposed;
	private bool _isSyncEnabled;
	private DateTimeOffset? _lastSyncTime;
	private PlcSyncStatus _status = PlcSyncStatus.Idle;

	public PlcSyncCoordinator(PlcTransactionExecutor transactionExecutor, IS7Service connectionService)
	{
		_executor = new PlcSyncExecutor(
			transactionExecutor,
			connectionService,
			_lock,
			status => Status = status,
			time => LastSyncTime = time);
	}

	public PlcSyncStatus Status
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
			PlcConnectionState connectionStateSnapshot;
			lock (_lock)
			{
				if (_status == value)
				{
					return;
				}
				_status = value;
				connectionStateSnapshot = _connectionState;
			}
			// Status and connectionState are both captured inside the lock, ensuring
			// the snapshot represents a consistent point in time.
			PublishSnapshot(connectionStateSnapshot);
		}
	}

	public DateTimeOffset? LastSyncTime
	{
		get
		{
			lock (_lock)
			{
				return _lastSyncTime;
			}
		}
		private set
		{
			lock (_lock)
			{
				_lastSyncTime = value;
			}
		}
	}

	public IObservable<Result<PlcSessionSnapshot>> PlcState => _subject;

	public void NotifyRecipeChanged(Recipe recipe, bool isValid)
	{
		if (_disposed)
		{
			return;
		}

		if (!isValid)
		{
			_executor.ClearPendingSnapshot();
			Status = PlcSyncStatus.OutOfSync;
			return;
		}

		_executor.OnRecipeChanged(recipe);
	}

	public void SetSyncEnabled(bool value)
	{
		PlcConnectionState connectionStateSnapshot;
		lock (_lock)
		{
			_isSyncEnabled = value;
			connectionStateSnapshot = _connectionState;
		}
		PublishSnapshot(connectionStateSnapshot);
	}

	public void UpdateConnectionState(PlcConnectionState state)
	{
		lock (_lock)
		{
			_connectionState = state;
		}
		PublishSnapshot(state);
	}

	public void Dispose()
	{
		lock (_lock)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
		}

		_executor.Dispose();
		_subject.OnCompleted();
		_subject.Dispose();
	}

	public void Reset()
	{
		_executor.Reset();
		Status = PlcSyncStatus.Disconnected;
	}

	public async Task WaitForPendingSyncAsync(CancellationToken ct = default)
	{
		await _executor.WaitForPendingSyncAsync(ct);
	}

	private void PublishSnapshot(PlcConnectionState connectionState)
	{
		PlcSyncStatus status;
		bool isSyncEnabled;
		string? errorMessage;
		bool disposed;

		lock (_lock)
		{
			disposed = _disposed;
			status = _status;
			isSyncEnabled = _isSyncEnabled;
			errorMessage = _executor.PendingErrorMessage;
		}

		if (disposed)
		{
			return;
		}

		var snapshot = new PlcSessionSnapshot(connectionState, status, isSyncEnabled);

		if (status == PlcSyncStatus.Failed)
		{
			TryPublish(
				Result.Fail<PlcSessionSnapshot>(new Error(errorMessage ?? "Sync failed"))
					.WithValue(snapshot));
			return;
		}

		if (status == PlcSyncStatus.Disconnected && isSyncEnabled)
		{
			TryPublish(
				Result.Fail<PlcSessionSnapshot>(new Error("PLC connection lost"))
					.WithValue(snapshot));
			return;
		}

		TryPublish(Result.Ok(snapshot));
	}

	private void TryPublish(Result<PlcSessionSnapshot> result)
	{
		if (!_disposed)
		{
			_subject.OnNext(result);
		}
	}
}
