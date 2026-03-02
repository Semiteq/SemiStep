using Shared.Entities;

namespace Domain.Ports;

public interface IS7ConnectionService
{
	bool IsConnected { get; }
	event Action<PlcConnectionState>? StateChanged;
	ValueTask DisposeAsync();
	Task ConnectAsync(PlcConnectionSettings settings, CancellationToken ct = default);
	Task DisconnectAsync(CancellationToken ct = default);
}
