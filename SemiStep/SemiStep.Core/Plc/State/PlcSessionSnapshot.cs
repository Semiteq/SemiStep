using FluentResults;

namespace SemiStep.Core.Plc.State;

public sealed record PlcSessionSnapshot(PlcConnectionState ConnectionState, PlcSyncStatus SyncStatus, bool IsSyncEnabled)
{
	public static readonly Result<PlcSessionSnapshot> InitialState = Result.Ok(
		new PlcSessionSnapshot(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, false));
}
