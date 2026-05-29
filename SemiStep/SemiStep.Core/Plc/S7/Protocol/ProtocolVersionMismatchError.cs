using FluentResults;

namespace SemiStep.Core.Plc.S7.Protocol;

internal sealed class ProtocolVersionMismatchError(int expected, int actual)
	: Error($"PLC protocol version {actual} does not match expected {expected}");
