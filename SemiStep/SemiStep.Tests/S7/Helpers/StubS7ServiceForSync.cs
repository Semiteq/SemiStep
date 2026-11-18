using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;

namespace SemiStep.Tests.S7.Helpers;

internal sealed class StubS7ServiceForSync(bool connected) : IS7Connection
{
	public bool IsConnected { get; private set; } = connected;

	public event Action<PlcConnectionState>? StateChanged
	{
		add { }
		remove { }
	}

	public void SetConnected(bool connected)
	{
		IsConnected = connected;
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
