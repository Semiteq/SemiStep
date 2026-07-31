using FluentResults;

namespace SemiStep.Core.Plc.Sync;

public sealed class PlcCommandFailedError()
	: Error("PLC command failed")
{
}
