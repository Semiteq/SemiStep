using System.Reactive.Subjects;

using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.Sync;

internal sealed class PlcSyncCoordinator : IPlcSyncService, IDisposable
{
	private readonly Lock _lock = new();
	private readonly BehaviorSubject<PlcSessionSnapshot> _subject = new(
		PlcSessionSnapshot.InitialState);
	private readonly Subject<IError> _faults = new();
	private readonly PlcSyncExecutor _executor;

	private PlcConnectionState _connectionState = PlcConnectionState.Disconnected;
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
			EmitFault,
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

	public IObservable<PlcSessionSnapshot> PlcState => _subject;

	public IObservable<IError> Faults => _faults;

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
		_faults.OnCompleted();
		_faults.Dispose();
	}

	public void ResetForDisable()
	{
		_executor.Reset();
		Status = PlcSyncStatus.Idle;
	}

	public void HandleConnectionLost()
	{
		_executor.Reset();
		EmitFault(new ConnectionLostError());
	}

	public async Task WaitForPendingSyncAsync(CancellationToken ct = default)
	{
		await _executor.WaitForPendingSyncAsync(ct);
	}

	private void EmitFault(IError fault)
	{
		lock (_lock)
		{
			if (_disposed)
			{
				return;
			}
			_faults.OnNext(fault);
		}
	}

	private void PublishSnapshot(PlcConnectionState connectionState)
	{
		lock (_lock)
		{
			if (_disposed)
			{
				return;
			}

			var snapshot = new PlcSessionSnapshot(connectionState, _status, _isSyncEnabled);
			_subject.OnNext(snapshot);
		}
	}
}
