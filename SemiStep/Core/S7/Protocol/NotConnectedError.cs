using FluentResults;

namespace S7.Protocol;

internal sealed class NotConnectedError(string message) : Error(message);
