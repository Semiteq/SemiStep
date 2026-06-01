namespace SemiStep.Core.Plc.S7;

/// <summary>
/// Serializes transport round-trips so concurrent reads and writes on the shared
/// S7 connection cannot interleave and corrupt multi-PDU framing.
/// </summary>
internal sealed class TransportSerializer : IDisposable
{
	private readonly SemaphoreSlim _gate = new(1, 1);

	public async Task RunAsync(Func<Task> operation, CancellationToken ct)
	{
		await _gate.WaitAsync(ct);
		try
		{
			await operation();
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken ct)
	{
		await _gate.WaitAsync(ct);
		try
		{
			return await operation();
		}
		finally
		{
			_gate.Release();
		}
	}

	public void Dispose()
	{
		_gate.Dispose();
	}
}
