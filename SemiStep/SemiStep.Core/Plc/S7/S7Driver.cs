using SemiStep.Core.Plc.Configuration;

using S7NetCpuType = S7.Net.CpuType;
using S7NetDataType = S7.Net.DataType;
using S7NetPlc = S7.Net.Plc;

namespace SemiStep.Core.Plc.S7;

internal sealed class S7Driver : IS7Driver
{
	private readonly TransportSerializer _serializer = new();

	// Written by connect/disconnect (UI or reconnect thread), read by the poll, keep-alive and sync threads;
	// every access goes through Volatile or Interlocked.
	private S7NetPlc? _plc;

	public bool IsConnected => Volatile.Read(ref _plc)?.IsConnected ?? false;

	public async ValueTask DisposeAsync()
	{
		await DisconnectAsync();
		_serializer.Dispose();
	}

	public async Task ConnectAsync(PlcConnectionSettings settings)
	{
		var plc = new S7NetPlc(
			S7NetCpuType.S71500,
			settings.IpAddress,
			settings.Port,
			(short)settings.Rack,
			(short)settings.Slot);
		Volatile.Write(ref _plc, plc);

		await plc.OpenAsync();
	}

	public Task DisconnectAsync()
	{
		Interlocked.Exchange(ref _plc, null)?.Close();

		return Task.CompletedTask;
	}

	public Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		return _serializer.RunAsync(
			() => RequirePlc().ReadBytesAsync(S7NetDataType.DataBlock, dbNumber, startByte, count, ct),
			ct);
	}

	public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		return _serializer.RunAsync(
			() => RequirePlc().WriteBytesAsync(S7NetDataType.DataBlock, dbNumber, startByte, data, ct),
			ct);
	}

	private S7NetPlc RequirePlc()
	{
		return Volatile.Read(ref _plc) ?? throw new DriverNotConnectedException();
	}
}
