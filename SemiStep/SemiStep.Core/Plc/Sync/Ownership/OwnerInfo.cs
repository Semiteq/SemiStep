namespace SemiStep.Core.Plc.Sync.Ownership;

public sealed record OwnerInfo(
	int ProcessId,
	string MachineName,
	string UserName,
	DateTimeOffset AcquiredUtc)
{
	public static OwnerInfo ForCurrentProcess()
	{
		return new OwnerInfo(
			ProcessId: Environment.ProcessId,
			MachineName: Environment.MachineName,
			UserName: Environment.UserName,
			AcquiredUtc: DateTimeOffset.UtcNow);
	}
}
