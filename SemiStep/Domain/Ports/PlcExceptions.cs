namespace Domain.Ports;

public sealed class PlcNotConnectedException(string message) : Exception(message);

public sealed class PlcBusyException(string message) : Exception(message);

public sealed class PlcSyncException(string message, PlcSyncError errorCode) : Exception(message)
{
	public PlcSyncError ErrorCode { get; } = errorCode;
}

public sealed class PlcSyncTimeoutException(string message) : Exception(message);

public sealed class PlcRecipeActiveException(string message) : Exception(message);
