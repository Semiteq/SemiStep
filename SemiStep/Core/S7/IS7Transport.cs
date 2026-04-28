namespace S7;

internal interface IS7Transport
{
	bool IsConnected { get; }

	Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default);

	Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default);
}
