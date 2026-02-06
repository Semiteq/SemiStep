namespace S7.Protocol;

public static class ManagingAreaLayout
{
	public const int PcStatusOffset = 0;
	public const int PcTransactionIdOffset = 2;
	public const int PcChecksumIntOffset = 6;
	public const int PcChecksumFloatOffset = 10;
	public const int PcChecksumStringOffset = 14;
	public const int PcRecipeLinesOffset = 18;
	public const int PlcStatusOffset = 22;
	public const int PlcErrorOffset = 24;
	public const int PlcStoredIdOffset = 26;
	public const int PlcChecksumIntOffset = 30;
	public const int PlcChecksumFloatOffset = 34;
	public const int PlcChecksumStringOffset = 38;

	public const int TotalSize = 42;

	public const int PcDataSize = 22;
}
