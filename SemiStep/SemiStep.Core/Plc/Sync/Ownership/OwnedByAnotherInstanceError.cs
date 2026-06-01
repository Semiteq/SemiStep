using FluentResults;

namespace SemiStep.Core.Plc.Sync.Ownership;

public sealed class OwnedByAnotherInstanceError : Error
{
	public OwnedByAnotherInstanceError(OwnerInfo holder)
		: base(BuildMessage(holder))
	{
		Holder = holder;
	}

	public OwnerInfo Holder { get; }

	private static string BuildMessage(OwnerInfo holder)
	{
		return $"PLC sync is owned by another instance "
			+ $"(user {holder.UserName}, since {holder.AcquiredUtc:HH:mm} UTC).";
	}
}
