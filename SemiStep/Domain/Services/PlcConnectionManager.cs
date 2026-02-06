using Domain.Ports;

using Serilog;

using Shared.Entities;

namespace Domain.Services;

public enum PlcConnectionState
{
	Disconnected,
	Connecting,
	Connected
}

public sealed class PlcConnectionManager : IAsyncDisposable
{
	private readonly IPlcConnection _connection;
	private readonly ILogger _logger;
	private readonly object _stateLock = new();

	private PlcConnectionSettings? _settings;
	private PlcConnectionState _state = PlcConnectionState.Disconnected;
	private CancellationTokenSource? _reconnectCts;
	private Task? _reconnectTask;
	private bool _autoReconnectEnabled;

	public PlcConnectionManager(IPlcConnection connection, ILogger logger)
	{
		_connection = connection;
		_logger = logger;
	}

	public event Action<PlcConnectionState>? StateChanged;

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

	public bool IsConnected => State == PlcConnectionState.Connected;

	public async Task ConnectAsync(PlcConnectionSettings settings, CancellationToken ct = default)
	{
		_settings = settings;
		_autoReconnectEnabled = true;

		await ConnectInternalAsync(ct);
	}

	public async Task DisconnectAsync(CancellationToken ct = default)
	{
		_autoReconnectEnabled = false;
		StopReconnectLoop();

		if (_connection.IsConnected)
		{
			await _connection.DisconnectAsync(ct);
		}

		State = PlcConnectionState.Disconnected;
		_logger.Information("Disconnected from PLC");
	}

	public async ValueTask DisposeAsync()
	{
		await DisconnectAsync();
		await _connection.DisposeAsync();
	}

	internal void OnConnectionLost()
	{
		State = PlcConnectionState.Disconnected;
		_logger.Warning("PLC connection lost");

		if (_autoReconnectEnabled && _settings is not null)
		{
			StartReconnectLoop();
		}
	}

	private async Task ConnectInternalAsync(CancellationToken ct)
	{
		if (_settings is null)
		{
			throw new InvalidOperationException("Connection settings not configured");
		}

		State = PlcConnectionState.Connecting;
		_logger.Information("Connecting to PLC at {IpAddress}...", _settings.IpAddress);

		try
		{
			await _connection.ConnectAsync(_settings, ct);
			State = PlcConnectionState.Connected;
			_logger.Information("Connected to PLC at {IpAddress}", _settings.IpAddress);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			State = PlcConnectionState.Disconnected;
			_logger.Error(ex, "Failed to connect to PLC at {IpAddress}", _settings.IpAddress);
			throw;
		}
	}

	private void StartReconnectLoop()
	{
		if (_reconnectTask is not null)
		{
			return;
		}

		_reconnectCts = new CancellationTokenSource();
		_reconnectTask = ReconnectLoopAsync(_reconnectCts.Token);
	}

	private void StopReconnectLoop()
	{
		_reconnectCts?.Cancel();
		_reconnectCts?.Dispose();
		_reconnectCts = null;
		_reconnectTask = null;
	}

	private async Task ReconnectLoopAsync(CancellationToken ct)
	{
		var delay = TimeSpan.FromSeconds(1);
		var maxDelay = TimeSpan.FromSeconds(30);

		while (!ct.IsCancellationRequested && _autoReconnectEnabled)
		{
			try
			{
				await Task.Delay(delay, ct);
				await ConnectInternalAsync(ct);

				_reconnectTask = null;
				return;
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				_logger.Warning(ex, "Reconnection attempt failed, retrying in {Delay}s", delay.TotalSeconds);
				delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
			}
		}
	}
}
