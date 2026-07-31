using FluentResults;

namespace SemiStep.Core.Plc.S7.Protocol;

public sealed class NotConnectedError()
	: Error("Not connected to PLC")
{
}
