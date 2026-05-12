using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;

namespace SemiStep.Core.Plc.S7;

internal interface IS7Driver : IS7Transport, IAsyncDisposable
{
	Task ConnectAsync(PlcConnectionSettings settings);

	Task DisconnectAsync();
}
