using SemiStep.Core.Plc.Sync.Ownership;

namespace SemiStep.Tests.Helpers;

/// <summary>
/// Lease handed out by <see cref="StubPlcSyncOwnership"/>. Counts dispose calls so tests
/// can assert the lease is released exactly once and that disposal is idempotent.
/// </summary>
public sealed class StubSyncOwnershipLease : ISyncOwnershipLease
{
	public StubSyncOwnershipLease(OwnerInfo owner)
	{
		Owner = owner;
	}

	public OwnerInfo Owner { get; }

	public int DisposeCallCount { get; private set; }

	public void Dispose()
	{
		DisposeCallCount++;
	}
}
