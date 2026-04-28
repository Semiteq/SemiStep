using FluentResults;

using TypesShared.Core;
using TypesShared.Plc;

namespace TypesShared.Domain;

public interface IS7Service : IAsyncDisposable
{
	bool IsConnected { get; }
	bool IsRecipeActive { get; }
	IObservable<PlcExecutionInfo> ExecutionState { get; }
	event Action<PlcConnectionState>? StateChanged;
	Task ConnectAsync(PlcConnectionSettings settings);
	Task DisconnectAsync();
	Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync();
	Task<Result<Recipe>> ReadRecipeFromPlcAsync();
}
