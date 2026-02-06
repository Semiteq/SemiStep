namespace S7.Protocol;

public static class ProtocolConstants
{
	public const ushort ProtocolVersion = 1;

	public const int HeaderDbNumber = 1;
	public const int ManagingDbNumber = 2;
	public const int IntDataDbNumber = 3;
	public const int FloatDataDbNumber = 4;
	public const int StringDataDbNumber = 5;
	public const int ExecutionDbNumber = 6;

	public const int MaxRetryAttempts = 3;
	public const int WritingTimeoutSeconds = 60;
	public const int CommitTimeoutSeconds = 30;
	public const int PollIntervalMs = 100;
}
