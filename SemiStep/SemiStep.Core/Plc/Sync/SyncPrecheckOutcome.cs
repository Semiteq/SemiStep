using FluentResults;

namespace SemiStep.Core.Plc.Sync;

internal abstract record SyncPrecheckOutcome
{
	private SyncPrecheckOutcome()
	{
	}

	internal sealed record Ready : SyncPrecheckOutcome;

	/// <summary>The attempt ends silently because the connection-lost path owns the fault.</summary>
	internal sealed record NotConnected : SyncPrecheckOutcome;

	internal sealed record Failed(IError Error) : SyncPrecheckOutcome;
}
