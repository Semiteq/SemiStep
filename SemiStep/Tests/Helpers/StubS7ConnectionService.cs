using Domain.Ports;

using Shared.Entities;

namespace Tests.Helpers;

public sealed class StubS7ConnectionService : IS7ConnectionService
{
	public bool IsConnected => false;

	public event Action<PlcConnectionState>? StateChanged;

	public Task ConnectAsync(PlcConnectionSettings settings, CancellationToken ct = default)
	{
		return Task.CompletedTask;
	}

	public Task DisconnectAsync(CancellationToken ct = default)
	{
		return Task.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}
}
