using FluentResults;

namespace SemiStep.Core.Plc.S7.Protocol;

public sealed class ProtocolVersionMismatchError(int expected, int actual)
	: Error($"PLC protocol version {actual} does not match expected {expected}")
{
	public int Expected { get; } = expected;

	public int Actual { get; } = actual;
}
