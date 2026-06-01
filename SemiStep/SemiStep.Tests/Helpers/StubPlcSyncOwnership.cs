using FluentResults;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Sync.Ownership;

namespace SemiStep.Tests.Helpers;

/// <summary>
/// Test double for <see cref="IPlcSyncOwnership"/>. Acquires successfully by default,
/// hands out a lease that records its dispose count, and can be switched to a refusal
/// mode that returns the configured <see cref="OwnedByAnotherInstanceError"/>.
/// </summary>
public sealed class StubPlcSyncOwnership : IPlcSyncOwnership
{
	private readonly OwnerInfo _owner;

	public StubPlcSyncOwnership()
	{
		_owner = new OwnerInfo(
			ProcessId: 1234,
			MachineName: "TEST-MACHINE",
			UserName: "test-user",
			AcquiredUtc: DateTimeOffset.UnixEpoch);
	}

	public bool ShouldRefuse { get; set; }

	public OwnerInfo RefusalOwner { get; set; } = new OwnerInfo(
		ProcessId: 9999,
		MachineName: "OTHER-MACHINE",
		UserName: "other-user",
		AcquiredUtc: DateTimeOffset.UnixEpoch);

	public int TryAcquireCallCount { get; private set; }

	public StubSyncOwnershipLease? LastLease { get; private set; }

	public Result<ISyncOwnershipLease> TryAcquire(PlcConnectionSettings endpoint)
	{
		TryAcquireCallCount++;

		if (ShouldRefuse)
		{
			return Result.Fail<ISyncOwnershipLease>(new OwnedByAnotherInstanceError(RefusalOwner));
		}

		var lease = new StubSyncOwnershipLease(_owner);
		LastLease = lease;
		return Result.Ok<ISyncOwnershipLease>(lease);
	}
}
