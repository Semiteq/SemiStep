using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Plc.State;

namespace Tests.S7.Helpers;

/// <summary>
/// A controllable in-memory driver stub for testing <see cref="S7.Facade.S7Service"/>
/// without a real PLC connection.
/// </summary>
internal sealed class FakeS7Driver : IS7Driver
{
	private readonly Dictionary<int, Exception> _dbReadExceptions = new();
	private readonly Dictionary<int, Func<int, int, byte[]>> _dbReadFactories = new();
	private bool _connected;

	public bool IsConnected => _connected;

	public List<(int DbNumber, int StartByte, int Count)> ReadLog { get; } = new();

	public List<(int DbNumber, int StartByte, byte[] Data)> WriteLog { get; } = new();

	/// <summary>
	/// Directly sets the connected state, bypassing <see cref="ConnectAsync"/> and
	/// <see cref="DisconnectAsync"/>. Useful for simulating mid-session connection drops.
	/// </summary>
	public void SetConnected(bool connected)
	{
		_connected = connected;
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
		_connected = true;
		return Task.CompletedTask;
	}

	public Task DisconnectAsync()
	{
		_connected = false;
		return Task.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		_connected = false;
		return ValueTask.CompletedTask;
	}

	public Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		ReadLog.Add((dbNumber, startByte, count));

		if (_dbReadExceptions.TryGetValue(dbNumber, out var exception))
		{
			throw exception;
		}

		if (_dbReadFactories.TryGetValue(dbNumber, out var factory))
		{
			return Task.FromResult(factory(startByte, count));
		}

		return Task.FromResult(new byte[count]);
	}

	public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		WriteLog.Add((dbNumber, startByte, (byte[])data.Clone()));
		return Task.CompletedTask;
	}
}
