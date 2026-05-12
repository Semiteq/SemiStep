using SemiStep.Core.Plc.S7;

namespace SemiStep.Tests.S7.Helpers;

/// <summary>
/// A transport that throws an <see cref="InvalidOperationException"/> on the first read of the
/// execution DB, then delegates all subsequent reads to an inner <see cref="IS7Transport"/>.
/// Used to verify that the execution monitor poll loop recovers from transient errors.
/// </summary>
internal sealed class FailFirstExecutionTransport(IS7Transport inner, int executionDbNumber) : IS7Transport
{
	private int _executionReadCount;

	public bool IsConnected => true;

	public async Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		if (dbNumber == executionDbNumber)
		{
			if (Interlocked.Increment(ref _executionReadCount) == 1)
			{
				throw new InvalidOperationException("Simulated transient read failure");
			}
		}

		return await inner.ReadBytesAsync(dbNumber, startByte, count, ct);
	}

	public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		return inner.WriteBytesAsync(dbNumber, startByte, data, ct);
	}
}
