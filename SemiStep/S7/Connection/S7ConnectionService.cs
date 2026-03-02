using Domain.Ports;

using Serilog;

using Shared.Entities;

namespace S7.Connection;

public sealed class S7ConnectionService(PlcTransport transport) : IAsyncDisposable, IS7ConnectionService
{
	private readonly Lock _stateLock = new();
	private bool _autoReconnectEnabled;
	private CancellationTokenSource? _reconnectCts;
	private Task? _reconnectTask;

	private PlcConnectionSettings? _settings;

	public PlcConnectionState State
	{
		get
		{
			lock (_stateLock)
			{
				return field;
			}
		}
		private set
		{
			lock (_stateLock)
			{
				if (field == value)
				{
					return;
				}
				field = value;
			}
			StateChanged?.Invoke(value);
		}
	} = PlcConnectionState.Disconnected;

	public bool IsConnected => State == PlcConnectionState.Connected;

	public async ValueTask DisposeAsync()
	{
		await DisconnectAsync();
		await transport.DisposeAsync();
	}

	public event Action<PlcConnectionState>? StateChanged;

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

		if (transport.IsConnected)
		{
			await transport.DisconnectAsync(ct);
		}

		State = PlcConnectionState.Disconnected;
		Log.Information("Disconnected from PLC");
	}

	internal void OnConnectionLost()
	{
		State = PlcConnectionState.Disconnected;
		Log.Warning("PLC connection lost");

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
		Log.Information("Connecting to PLC at {IpAddress}...", _settings.IpAddress);

		try
		{
			await transport.ConnectAsync(_settings, ct);
			State = PlcConnectionState.Connected;
			Log.Information("Connected to PLC at {IpAddress}", _settings.IpAddress);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			State = PlcConnectionState.Disconnected;
			Log.Error(ex, "Failed to connect to PLC at {IpAddress}", _settings.IpAddress);

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
				Log.Warning(ex, "Reconnection attempt failed, retrying in {Delay}s", delay.TotalSeconds);
				delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
			}
		}
	}
}
