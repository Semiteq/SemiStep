namespace SemiStep.Core.Plc.State;

public enum PlcSyncStatus
{
	Idle,
	Syncing,
	Synced,
	OutOfSync,
	Failed
}
