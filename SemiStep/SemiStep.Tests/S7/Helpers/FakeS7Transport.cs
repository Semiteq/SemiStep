using SemiStep.Core.Plc.S7;

namespace SemiStep.Tests.S7.Helpers;

/// <summary>
/// A controllable in-memory transport stub for testing <see cref="SemiStep.Core.Plc.Sync.PlcTransactionExecutor"/>
/// without a real PLC connection.
/// </summary>
internal sealed class FakeS7Transport : IS7Transport
{
	private readonly Dictionary<(int DbNumber, int StartByte, int Count), byte[]> _readResponses = [];
	private readonly Dictionary<int, Func<int, int, byte[]>> _dbReadFactories = [];

	/// <summary>When set, every write throws this exception, simulating a transport-level write failure.</summary>
	public Exception? WriteException { get; set; }

	/// <summary>Ordered log of every write call received.</summary>
	public List<(int DbNumber, int StartByte, byte[] Data)> WriteLog { get; } = [];

	/// <summary>Ordered log of every read call received (dbNumber, startByte, count).</summary>
	public List<(int DbNumber, int StartByte, int Count)> ReadLog { get; } = [];

	/// <summary>When set, a read awaits the returned task before answering.</summary>
	public Func<int, CancellationToken, Task>? BeforeRead { get; set; }

	public bool IsConnected { get; private set; } = true;

	public void SetConnected(bool connected)
	{
		IsConnected = connected;
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

	public async Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		ReadLog.Add((dbNumber, startByte, count));

		if (BeforeRead is { } beforeRead)
		{
			await beforeRead(dbNumber, ct);
		}

		var key = (dbNumber, startByte, count);
		return _readResponses.TryGetValue(key, out var response)
			? response
			: _dbReadFactories.TryGetValue(dbNumber, out var factory)
			? factory(startByte, count)
			: new byte[count];
	}

	public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		if (WriteException is not null)
		{
			throw WriteException;
		}

		WriteLog.Add((dbNumber, startByte, (byte[])data.Clone()));
		return Task.CompletedTask;
	}
}
