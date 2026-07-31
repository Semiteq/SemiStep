using FluentResults;

namespace SemiStep.Core.Plc.Sync;

public sealed class ConnectionLostError()
	: Error("PLC connection lost")
{
}
