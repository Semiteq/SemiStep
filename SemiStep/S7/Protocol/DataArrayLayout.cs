namespace S7.Protocol;

public static class DataArrayLayout
{
	public const int CapacityOffset = 0;
	public const int CurrentSizeOffset = 4;
	public const int DataStartOffset = 8;

	public const int IntElementSize = 4;
	public const int FloatElementSize = 4;
	public const int WStringMaxChars = 32;
	public const int WStringHeaderSize = 4;
	public const int WStringElementSize = WStringHeaderSize + WStringMaxChars * 2;
}
