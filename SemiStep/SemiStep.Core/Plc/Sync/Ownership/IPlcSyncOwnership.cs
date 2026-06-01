using FluentResults;

using SemiStep.Core.Plc.Configuration;

namespace SemiStep.Core.Plc.Sync.Ownership;

public interface IPlcSyncOwnership
{
	Result<ISyncOwnershipLease> TryAcquire(PlcConnectionSettings endpoint);
}
