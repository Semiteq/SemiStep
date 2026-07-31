using FluentResults;

namespace SemiStep.Core.Plc.Sync;

public sealed class WriteVerificationFailedError(int attempts)
	: Error($"Recipe write verification failed after {attempts} attempts")
{
	public int Attempts { get; } = attempts;
}
