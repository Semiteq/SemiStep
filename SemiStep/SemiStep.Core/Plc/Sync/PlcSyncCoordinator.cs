using System.Reactive.Subjects;

using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.Sync;

internal sealed class PlcSyncCoordinator : IPlcSyncService, IDisposable
{
	private readonly Lock _lock = new();
	private readonly BehaviorSubject<Result<PlcSessionSnapshot>> _subject = new(
		PlcSessionSnapshot.InitialState);
	private readonly PlcSyncExecutor _executor;

	private PlcConnectionState _connectionState = PlcConnectionState.Disconnected;
	private bool _connectionLost;
	private volatile bool _disposed;
	private bool _isSyncEnabled;
	private DateTimeOffset? _lastSyncTime;
	private PlcSyncStatus _status = PlcSyncStatus.Idle;

	public PlcSyncCoordinator(
		PlcTransactionExecutor transactionExecutor,
		IS7Connection connection,
		ILoggerFactory loggerFactory)
	{
		_executor = new PlcSyncExecutor(
			transactionExecutor,
			connection,
			_lock,
			status => Status = status,
			time => LastSyncTime = time,
			loggerFactory.CreateLogger<PlcSyncExecutor>());
	}

	public bool IsSyncEnabled
	{
		get
		{
			lock (_lock)
			{
				return _isSyncEnabled;
			}
		}
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
			if (value)
			{
				_connectionLost = false;
			}
			connectionStateSnapshot = _connectionState;
		}
		PublishSnapshot(connectionStateSnapshot);
	}

	public void UpdateConnectionState(PlcConnectionState state)
	{
		lock (_lock)
		{
			_connectionState = state;
			if (state == PlcConnectionState.Connected)
			{
				_connectionLost = false;
			}
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

	public void ResetForDisable()
	{
		_executor.Reset();
		lock (_lock)
		{
			_connectionLost = false;
		}
		Status = PlcSyncStatus.Idle;
	}

	public void HandleConnectionLost()
	{
		_executor.Reset();
		PlcConnectionState connectionStateSnapshot;
		lock (_lock)
		{
			_connectionLost = true;
			connectionStateSnapshot = _connectionState;
		}
		PublishSnapshot(connectionStateSnapshot);
	}

	public async Task WaitForPendingSyncAsync(CancellationToken ct = default)
	{
		await _executor.WaitForPendingSyncAsync(ct);
	}

	private void PublishSnapshot(PlcConnectionState connectionState)
	{
		PlcSyncStatus status;
		bool isSyncEnabled;
		bool connectionLost;
		string? errorMessage;
		bool disposed;

		lock (_lock)
		{
			disposed = _disposed;
			status = _status;
			isSyncEnabled = _isSyncEnabled;
			connectionLost = _connectionLost;
			errorMessage = _executor.PendingErrorMessage;
		}

		if (disposed)
		{
			return;
		}

		var snapshot = new PlcSessionSnapshot(connectionState, status, isSyncEnabled);

		if (status == PlcSyncStatus.Failed)
		{
			TryPublish(Result.Fail<PlcSessionSnapshot>(new Error(errorMessage ?? "Sync failed")));
			return;
		}

		if (connectionLost && isSyncEnabled)
		{
			TryPublish(Result.Fail<PlcSessionSnapshot>(new Error("PLC connection lost")));
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
