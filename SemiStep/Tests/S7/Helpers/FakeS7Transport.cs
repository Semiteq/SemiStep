using SemiStep.Core.Plc.S7;

namespace Tests.S7.Helpers;

/// <summary>
/// A controllable in-memory transport stub for testing <see cref="PlcTransactionExecutor"/>
/// without a real PLC connection.
/// </summary>
internal sealed class FakeS7Transport : IS7Transport
{
	private readonly Dictionary<(int DbNumber, int StartByte, int Count), byte[]> _readResponses = new();
	private readonly Dictionary<int, Func<int, int, byte[]>> _dbReadFactories = new();
	private bool _connected = true;

	/// <summary>Ordered log of every write call received.</summary>
	public List<(int DbNumber, int StartByte, byte[] Data)> WriteLog { get; } = new();

	/// <summary>Ordered log of every read call received (dbNumber, startByte, count).</summary>
	public List<(int DbNumber, int StartByte, int Count)> ReadLog { get; } = new();

	public bool IsConnected => _connected;

	public void SetConnected(bool connected)
	{
		_connected = connected;
	}

	/// <summary>
	/// Registers a byte array to return when <see cref="ReadBytesAsync"/> is called with the
	/// given parameters.
	/// </summary>
	public void SetReadResponse(int dbNumber, int startByte, int count, byte[] response)
	{
		_readResponses[(dbNumber, startByte, count)] = response;
	}

	/// <summary>
	/// Registers a catch-all response for any read from the given DB number, regardless of
	/// offset and count. More-specific key registrations take precedence.
	/// </summary>
	public void SetReadResponseForDb(int dbNumber, Func<int, int, byte[]> responseFactory)
	{
		_dbReadFactories[dbNumber] = responseFactory;
	}

	public Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		ReadLog.Add((dbNumber, startByte, count));

		var key = (dbNumber, startByte, count);
		if (_readResponses.TryGetValue(key, out var response))
		{
			return Task.FromResult(response);
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
