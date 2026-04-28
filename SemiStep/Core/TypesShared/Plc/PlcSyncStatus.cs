namespace TypesShared.Plc;

public enum PlcSyncStatus
{
	Idle,
	Syncing,
	Synced,
	OutOfSync,
	Failed,
	Disconnected
}
