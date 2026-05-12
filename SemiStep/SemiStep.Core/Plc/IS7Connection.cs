using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;

namespace SemiStep.Core.Plc;

public interface IS7Connection : IAsyncDisposable
{
	bool IsConnected { get; }
	event Action<PlcConnectionState>? StateChanged;
	Task ConnectAsync(PlcConnectionSettings settings);
	Task DisconnectAsync();
}
