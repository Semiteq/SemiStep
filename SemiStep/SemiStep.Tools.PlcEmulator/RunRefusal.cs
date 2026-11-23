using FluentResults;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>An operator command the run engine refused; the console prints the message.</summary>
internal sealed class RunRefusal(RunRefusalReason reason, string message) : Error(message)
{
	public RunRefusalReason Reason { get; } = reason;
}
