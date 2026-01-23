using Domain.Ports;

namespace S7;

public sealed class ModbusTcpConnection : IPlcConnection
{
	private readonly string _host;
	private readonly int _port;
	private bool _isConnected;

	public ModbusTcpConnection(string host = "127.0.0.1", int port = 502)
	{
		_host = host;
		_port = port;
	}

	public bool IsConnected => _isConnected;

	public async Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		await Task.Delay(100, cancellationToken);
		_isConnected = true;
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken = default)
	{
		await Task.Delay(50, cancellationToken);
		_isConnected = false;
	}

	public async Task<Recipe.Entities.Recipe> ReadRecipeAsync(CancellationToken cancellationToken = default)
	{
		EnsureConnected();
		await Task.Delay(100, cancellationToken);
		return Recipe.Entities.Recipe.Empty;
	}

	public async Task WriteRecipeAsync(Recipe.Entities.Recipe recipe, CancellationToken cancellationToken = default)
	{
		EnsureConnected();
		await Task.Delay(100, cancellationToken);
	}

	public async Task<PlcStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		EnsureConnected();
		await Task.Delay(50, cancellationToken);
		return new PlcStatus(
			IsProcessing: false,
			IsRecipeValid: true,
			CurrentStep: 0,
			ExecutionState: ExecutionState.Stopped);
	}

	public async ValueTask DisposeAsync()
	{
		if (_isConnected)
		{
			await DisconnectAsync();
		}
	}

	private void EnsureConnected()
	{
		if (!_isConnected)
		{
			throw new InvalidOperationException("Not connected to PLC");
		}
	}
}
