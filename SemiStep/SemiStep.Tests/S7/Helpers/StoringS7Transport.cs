using SemiStep.Core.Plc.S7;

namespace SemiStep.Tests.S7.Helpers;

/// <summary>An in-memory transport that keeps written bytes per DB at any offset and serves reads from them.</summary>
internal sealed class StoringS7Transport : IS7Transport
{
	private readonly Dictionary<int, byte[]> _dataBlocks = [];

	public bool IsConnected => true;

	public Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		var result = new byte[count];
		if (_dataBlocks.TryGetValue(dbNumber, out var stored) && startByte < stored.Length)
		{
			Array.Copy(stored, startByte, result, 0, Math.Min(count, stored.Length - startByte));
		}

		return Task.FromResult(result);
	}

	public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		var required = startByte + data.Length;
		var stored = _dataBlocks.GetValueOrDefault(dbNumber) ?? [];
		if (stored.Length < required)
		{
			Array.Resize(ref stored, required);
		}

		Array.Copy(data, 0, stored, startByte, data.Length);
		_dataBlocks[dbNumber] = stored;
		return Task.CompletedTask;
	}
}
