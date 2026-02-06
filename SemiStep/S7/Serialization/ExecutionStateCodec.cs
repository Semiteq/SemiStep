using System.Buffers.Binary;

using Domain.Ports;

using S7.Protocol;

namespace S7.Serialization;

internal static class ExecutionStateCodec
{
	public static PlcExecutionState Decode(byte[] data)
	{
		if (data.Length < ExecutionAreaLayout.TotalSize)
		{
			throw new ArgumentException(
				$"Data length {data.Length} is less than expected {ExecutionAreaLayout.TotalSize}");
		}

		return new PlcExecutionState(
			RecipeActive: data[ExecutionAreaLayout.RecipeActiveOffset] != 0 || data[ExecutionAreaLayout.RecipeActiveOffset + 1] != 0,
			ActualLine: BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(ExecutionAreaLayout.ActualLineOffset)),
			StepCurrentTime: BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(ExecutionAreaLayout.StepCurrentTimeOffset))),
			ForLoopCount1: BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(ExecutionAreaLayout.ForLoopCount1Offset)),
			ForLoopCount2: BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(ExecutionAreaLayout.ForLoopCount2Offset)),
			ForLoopCount3: BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(ExecutionAreaLayout.ForLoopCount3Offset)));
	}
}
