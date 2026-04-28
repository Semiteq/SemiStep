using TypesShared.Plc;

namespace S7;

internal interface IS7Driver : IS7Transport, IAsyncDisposable
{
	Task ConnectAsync(PlcConnectionSettings settings);

	Task DisconnectAsync();
}
