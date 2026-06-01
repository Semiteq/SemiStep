namespace SemiStep.Core.Plc.Sync.Ownership;

public interface ISyncOwnershipLease : IDisposable
{
	OwnerInfo Owner { get; }
}
