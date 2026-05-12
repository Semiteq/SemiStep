using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Plc.State;

namespace SemiStep.Tests.S7.Helpers;

/// <summary>
/// A transport that returns a configurable <see cref="PlcExecutionInfo"/> encoded as bytes
/// for execution DB reads, and returns empty arrays for all other DBs.
/// </summary>
internal sealed class FakeExecutionTransport : IS7Transport
{
	private readonly PlcProtocolLayout _layout;
	private readonly PlcExecutionInfo _executionState;
	private int _executionReadCount;

	public FakeExecutionTransport(PlcProtocolLayout layout, PlcExecutionInfo executionState)
	{
		_layout = layout;
		_executionState = executionState;
	}

	public bool IsConnected => true;

	public int ExecutionReadCount => _executionReadCount;

	public Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();

		if (dbNumber == _layout.ExecutionDb.DbNumber)
		{
			Interlocked.Increment(ref _executionReadCount);
			return Task.FromResult(EncodeExecutionState());
		}

		return Task.FromResult(new byte[count]);
	}

	public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		return Task.CompletedTask;
	}

	private byte[] EncodeExecutionState()
	{
		var layout = _layout.ExecutionDb;
		var bytes = new byte[layout.TotalSize];

		bytes[layout.RecipeActiveOffset] = _executionState.RecipeActive ? (byte)1 : (byte)0;
		bytes[layout.RecipeActiveOffset + 1] = 0;
		System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(
			bytes.AsSpan(layout.ActualLineOffset), _executionState.ActualLine);
		var floatBits = BitConverter.SingleToInt32Bits(_executionState.StepCurrentTime);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(
			bytes.AsSpan(layout.StepCurrentTimeOffset), floatBits);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(
			bytes.AsSpan(layout.ForLoopCount1Offset), _executionState.ForLoopCount1);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(
			bytes.AsSpan(layout.ForLoopCount2Offset), _executionState.ForLoopCount2);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(
			bytes.AsSpan(layout.ForLoopCount3Offset), _executionState.ForLoopCount3);

		return bytes;
	}
}
