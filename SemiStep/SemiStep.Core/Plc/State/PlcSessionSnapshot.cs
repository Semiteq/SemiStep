namespace SemiStep.Core.Plc.State;

public sealed record PlcSessionSnapshot(PlcConnectionState ConnectionState, PlcSyncStatus SyncStatus, bool IsSyncEnabled)
{
	public static readonly PlcSessionSnapshot InitialState =
		new(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, false);
}
