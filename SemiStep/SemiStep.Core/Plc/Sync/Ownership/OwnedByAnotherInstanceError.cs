using FluentResults;

namespace SemiStep.Core.Plc.Sync.Ownership;

public sealed class OwnedByAnotherInstanceError(OwnerInfo holder) : Error(BuildMessage(holder))
{
	public OwnerInfo Holder { get; } = holder;

	private static string BuildMessage(OwnerInfo holder)
	{
		return $"PLC sync is owned by another instance "
			+ $"(user {holder.UserName}, since {holder.AcquiredUtc:HH:mm} UTC).";
	}
}
