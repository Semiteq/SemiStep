using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7;

namespace SemiStep.Tests.S7.Helpers;

/// <summary>
/// A controllable in-memory driver stub for testing <see cref="S7Service"/>
/// without a real PLC connection.
/// </summary>
internal sealed class FakeS7Driver : IS7Driver
{
	private readonly Dictionary<int, Exception> _dbReadExceptions = [];
	private readonly Dictionary<int, Func<int, int, byte[]>> _dbReadFactories = [];

	public bool IsConnected { get; private set; }

	public List<(int DbNumber, int StartByte, int Count)> ReadLog { get; } = [];

	public List<(int DbNumber, int StartByte, byte[] Data)> WriteLog { get; } = [];

	/// <summary>
	/// Directly sets the connected state, bypassing <see cref="ConnectAsync"/> and
	/// <see cref="DisconnectAsync"/>. Useful for simulating mid-session connection drops.
	/// </summary>
	public void SetConnected(bool connected)
	{
		IsConnected = connected;
	}

	/// <summary>
	/// Configures reads for the given DB number to throw the specified exception.
	/// </summary>
	public void SetReadExceptionForDb(int dbNumber, Exception exception)
	{
		_dbReadExceptions[dbNumber] = exception;
	}

	/// <summary>
	/// Registers a catch-all response factory for any read from the given DB number.
	/// </summary>
	public void SetReadResponseForDb(int dbNumber, Func<int, int, byte[]> responseFactory)
	{
		_dbReadFactories[dbNumber] = responseFactory;
	}

	public Task ConnectAsync(PlcConnectionSettings settings)
	{
		IsConnected = true;
		return Task.CompletedTask;
	}

	public Task DisconnectAsync()
	{
		IsConnected = false;
		return Task.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		IsConnected = false;
		return ValueTask.CompletedTask;
	}

	public Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		ReadLog.Add((dbNumber, startByte, count));

		return _dbReadExceptions.TryGetValue(dbNumber, out var exception)
			? throw exception
			: _dbReadFactories.TryGetValue(dbNumber, out var factory)
			? Task.FromResult(factory(startByte, count))
			: Task.FromResult(new byte[count]);
	}

	public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		WriteLog.Add((dbNumber, startByte, (byte[])data.Clone()));
		return Task.CompletedTask;
	}
}
