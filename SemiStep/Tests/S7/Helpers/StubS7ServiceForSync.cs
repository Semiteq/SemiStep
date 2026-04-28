using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;

namespace Tests.S7.Helpers;

internal sealed class StubS7ServiceForSync : IS7Connection
{
	private bool _connected;

	public StubS7ServiceForSync(bool connected)
	{
		_connected = connected;
	}

	public bool IsConnected => _connected;

	public event Action<PlcConnectionState>? StateChanged
	{
		add { }
		remove { }
	}

	public void SetConnected(bool connected)
	{
		_connected = connected;
	}

	public Task ConnectAsync(PlcConnectionSettings settings)
	{
		return Task.CompletedTask;
	}

	public Task DisconnectAsync()
	{
		return Task.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}
}
