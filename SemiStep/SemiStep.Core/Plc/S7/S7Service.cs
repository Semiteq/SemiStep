using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.S7;

internal sealed class S7Service(
	IS7Driver transport,
	PlcExecutionMonitor executionMonitor,
	PlcTransactionExecutor transactionExecutor,
	PlcConfiguration plcConfiguration,
	ILogger<S7Service> logger)
	: IS7Connection, IS7Reader, IS7ExecutionStream
{
	private static readonly TimeSpan _reconnectInitialDelay = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan _reconnectMaxDelay = TimeSpan.FromSeconds(30);

	private readonly ILogger<S7Service> _logger = logger;
	private readonly Lock _stateLock = new();

	// The container calls DisposeAsync once per registration that resolved this instance.
	private int _disposeStarted;

	// Every field below is guarded by _stateLock: the UI connect/disconnect path writes them, and the
	// keep-alive, poll and reconnect loops (thread pool) read and write them.
	private PlcConnectionState _state = PlcConnectionState.Disconnected;
	private bool _autoReconnectEnabled;
	private PlcConnectionSettings? _settings;
	private CancellationTokenSource? _reconnectCts;
	private Task? _reconnectTask;
	private CancellationTokenSource? _keepAliveCts;
	private Task? _keepAliveTask;

	public PlcConnectionState State
	{
		get
		{
			lock (_stateLock)
			{
				return _state;
			}
		}
		private set
		{
			lock (_stateLock)
			{
				if (_state == value)
				{
					return;
				}
				_state = value;
			}
			StateChanged?.Invoke(value);
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposeStarted, 1) == 1)
		{
			return;
		}

		await DisconnectAsync();
		executionMonitor.Dispose();
	}

	public bool IsConnected => State == PlcConnectionState.Connected;

	public bool IsRecipeActive => executionMonitor.LastKnown.RecipeActive;

	public IObservable<PlcExecutionInfo> ExecutionState => executionMonitor.State;

	public DateTimeOffset? LastExchangeTime => transactionExecutor.LastExchangeTime;

	public event Action<PlcConnectionState>? StateChanged;

	public async Task ConnectAsync(PlcConnectionSettings settings)
	{
		lock (_stateLock)
		{
			_settings = settings;
			_autoReconnectEnabled = true;
		}

		await ConnectInternalAsync();
	}

	public async Task DisconnectAsync()
	{
		Task? reconnectTaskToAwait;
		lock (_stateLock)
		{
			_autoReconnectEnabled = false;
			reconnectTaskToAwait = StopReconnectLoop();
		}

		if (reconnectTaskToAwait is not null)
		{
			try
			{
				await reconnectTaskToAwait;
			}
			catch (OperationCanceledException)
			{
			}
		}

		var keepAliveTaskToAwait = StopKeepAlive();
		if (keepAliveTaskToAwait is not null)
		{
			try
			{
				await keepAliveTaskToAwait;
			}
			catch (OperationCanceledException)
			{
			}
		}

		await executionMonitor.StopAsync();

		if (transport.IsConnected)
		{
			await transport.DisconnectAsync();
		}

		State = PlcConnectionState.Disconnected;
		_logger.LogInformation("Disconnected from PLC");
	}

	public async Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync(CancellationToken ct = default)
	{
		var result = await transactionExecutor.ReadManagingAreaAsync(ct);
		if (result.IsFailed)
		{
			_logger.LogWarning("Failed to read managing area from PLC: {Reason}", result.Errors.Count > 0 ? result.Errors[0].Message : "(no message)");
			return result.ToResult<PlcManagingAreaState>();
		}
		return result;
	}

	public async Task<Result<Recipe>> ReadRecipeFromPlcAsync(CancellationToken ct = default)
	{
		var result = await transactionExecutor.ReadRecipeFromPlcAsync(ct);
		if (result.IsFailed)
		{
			_logger.LogWarning("Failed to read recipe from PLC: {Reason}", result.Errors.Count > 0 ? result.Errors[0].Message : "(no message)");
		}
		return result;
	}

	public async Task<Result<int>> ReadProtocolVersionAsync(CancellationToken ct = default)
	{
		var result = await transactionExecutor.ReadProtocolVersionAsync(ct);
		if (result.IsFailed)
		{
			_logger.LogWarning("Failed to read protocol version from PLC: {Reason}", result.Errors.Count > 0 ? result.Errors[0].Message : "(no message)");
		}
		return result;
	}

	internal void OnConnectionLost()
	{
		lock (_stateLock)
		{
			if (_state != PlcConnectionState.Connected)
			{
				return;
			}

			_state = PlcConnectionState.Disconnected;
		}

		StateChanged?.Invoke(PlcConnectionState.Disconnected);

		_ = StopKeepAlive();
		executionMonitor.Stop();
		_logger.LogWarning("PLC connection lost");

		StartReconnectLoop();
	}

	private async Task ConnectInternalAsync()
	{
		PlcConnectionSettings? settings;
		lock (_stateLock)
		{
			settings = _settings;
		}

		if (settings is null)
		{
			throw new InvalidOperationException("Connection settings not configured");
		}

		State = PlcConnectionState.Connecting;
		_logger.LogInformation("Connecting to PLC at {IpAddress}...", settings.IpAddress);

		try
		{
			await transport.ConnectAsync(settings);
			State = PlcConnectionState.Connected;
			_logger.LogInformation("Connected to PLC at {IpAddress}", settings.IpAddress);

			executionMonitor.Start();
			StartKeepAlive();
		}
		catch (Exception ex)
		{
			State = PlcConnectionState.Disconnected;
			_logger.LogError(ex, "Failed to connect to PLC at {IpAddress}", settings.IpAddress);

			throw;
		}
	}

	private void StartKeepAlive()
	{
		lock (_stateLock)
		{
			// A loss between the Connected flip and this call has already run StopKeepAlive.
			if (_keepAliveTask is not null || _state != PlcConnectionState.Connected)
			{
				return;
			}

			var keepAliveCts = new CancellationTokenSource();
			// Taken now: a stop can dispose the source before the loop starts, and .Token would then throw.
			var token = keepAliveCts.Token;
			_keepAliveCts = keepAliveCts;
			_keepAliveTask = Task.Run(() => KeepAliveLoopAsync(token), CancellationToken.None);
		}
	}

	private Task? StopKeepAlive()
	{
		lock (_stateLock)
		{
			_keepAliveCts?.Cancel();
			_keepAliveCts?.Dispose();
			_keepAliveCts = null;

			var taskToAwait = _keepAliveTask;
			_keepAliveTask = null;

			return taskToAwait;
		}
	}

	private async Task KeepAliveLoopAsync(CancellationToken ct)
	{
		var intervalMs = plcConfiguration.ProtocolSettings.KeepAliveIntervalMs;

		while (!ct.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(intervalMs, ct);
				await transactionExecutor.KeepAliveReadAsync(ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				_logger.LogWarning("Keep-alive probe failed, connection assumed lost: {Message}", ex.Message);
				OnConnectionLost();

				return;
			}
		}
	}

	private void StartReconnectLoop()
	{
		lock (_stateLock)
		{
			if (!_autoReconnectEnabled || _state != PlcConnectionState.Disconnected || _reconnectTask is not null)
			{
				return;
			}

			var reconnectCts = new CancellationTokenSource();
			// Taken now: a stop can dispose the source before the loop starts, and .Token would then throw.
			var token = reconnectCts.Token;
			_reconnectCts = reconnectCts;
			_reconnectTask = Task.Run(() => ReconnectLoopAsync(reconnectCts, token), CancellationToken.None);
		}
	}

	private Task? StopReconnectLoop()
	{
		lock (_stateLock)
		{
			_reconnectCts?.Cancel();
			_reconnectCts?.Dispose();
			_reconnectCts = null;

			var taskToAwait = _reconnectTask;
			_reconnectTask = null;

			return taskToAwait;
		}
	}

	private async Task ReconnectLoopAsync(CancellationTokenSource reconnectCts, CancellationToken ct)
	{
		try
		{
			await RetryConnectAsync(ct);
		}
		finally
		{
			ReleaseReconnectLoop(reconnectCts);
		}
	}

	private void ReleaseReconnectLoop(CancellationTokenSource reconnectCts)
	{
		lock (_stateLock)
		{
			if (_reconnectCts != reconnectCts)
			{
				return;
			}

			_reconnectCts = null;
			_reconnectTask = null;

			// A loss between the Connected flip and this release found this loop still registered.
			StartReconnectLoop();
		}

		reconnectCts.Dispose();
	}

	private async Task RetryConnectAsync(CancellationToken ct)
	{
		var delay = _reconnectInitialDelay;

		while (!ct.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(delay, ct);
				await ConnectInternalAsync();

				return;
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				_logger.LogWarning("Reconnection attempt failed, retrying in {Delay}s: {Message}", delay.TotalSeconds, ex.Message);
				delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, _reconnectMaxDelay.TotalSeconds));
			}
		}
	}
}
