using FluentResults;

namespace SemiStep.Core.Plc.S7.Protocol;

internal sealed class NotConnectedError(string message) : Error(message);
