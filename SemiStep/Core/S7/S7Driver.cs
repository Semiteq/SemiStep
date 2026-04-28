using S7.Net;

using TypesShared.Plc;

namespace S7;

internal sealed class S7Driver : IS7Driver
{
	private Plc? _plc;

	public bool IsConnected => _plc?.IsConnected ?? false;

	public async ValueTask DisposeAsync()
	{
		if (_plc is not null)
		{
			await DisconnectAsync();
		}
	}

	public async Task ConnectAsync(PlcConnectionSettings settings)
	{
		_plc = new Plc(
			CpuType.S71500,
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

	public async Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		return await _plc!.ReadBytesAsync(DataType.DataBlock, dbNumber, startByte, count, ct);
	}

	public async Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		await _plc!.WriteBytesAsync(DataType.DataBlock, dbNumber, startByte, data, ct);
	}
}
