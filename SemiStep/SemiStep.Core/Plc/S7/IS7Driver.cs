using SemiStep.Core.Plc.Configuration;

namespace SemiStep.Core.Plc.S7;

internal interface IS7Driver : IS7Transport, IAsyncDisposable
{
	Task ConnectAsync(PlcConnectionSettings settings);

	Task DisconnectAsync();
}
