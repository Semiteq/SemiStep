using FluentResults;

namespace TypesShared.Plc;

public sealed record PlcSessionSnapshot(PlcConnectionState ConnectionState, PlcSyncStatus SyncStatus, bool IsSyncEnabled)
{
	public static readonly Result<PlcSessionSnapshot> InitialState = Result.Ok(
		new PlcSessionSnapshot(PlcConnectionState.Disconnected, PlcSyncStatus.Disconnected, false));
}
