using SemiStep.Core.Plc.Configuration;

using S7NetCpuType = global::S7.Net.CpuType;
using S7NetDataType = global::S7.Net.DataType;
using S7NetPlc = global::S7.Net.Plc;

namespace SemiStep.Core.Plc.S7;

internal sealed class S7Driver : IS7Driver
{
	private readonly TransportSerializer _serializer = new();
	private S7NetPlc? _plc;

	public bool IsConnected => _plc?.IsConnected ?? false;

	public async ValueTask DisposeAsync()
	{
		if (_plc is not null)
		{
			await DisconnectAsync();
		}

		_serializer.Dispose();
	}

	public async Task ConnectAsync(PlcConnectionSettings settings)
	{
		_plc = new S7NetPlc(
			S7NetCpuType.S71500,
			settings.IpAddress,
			settings.Port,
			(short)settings.Rack,
			(short)settings.Slot);

		await _plc.OpenAsync();
	}

	public Task DisconnectAsync()
	{
		_plc?.Close();
		_plc = null;

		return Task.CompletedTask;
	}

	public Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		return _serializer.RunAsync(
			() => _plc!.ReadBytesAsync(S7NetDataType.DataBlock, dbNumber, startByte, count, ct),
			ct);
	}

	public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		return _serializer.RunAsync(
			() => _plc!.WriteBytesAsync(S7NetDataType.DataBlock, dbNumber, startByte, data, ct),
			ct);
	}
}
