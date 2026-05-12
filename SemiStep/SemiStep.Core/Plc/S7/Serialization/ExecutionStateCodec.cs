using System.Buffers.Binary;

using FluentResults;

using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.State;

namespace SemiStep.Core.Plc.S7.Serialization;

internal sealed class ExecutionStateCodec
{
	private readonly ExecutionDbLayout _layout;

	public ExecutionStateCodec(ExecutionDbLayout layout)
	{
		_layout = layout;
	}

	public Result<PlcExecutionInfo> Decode(byte[] data)
	{
		if (data.Length < _layout.TotalSize)
		{
			return Result.Fail(
				$"Execution state data length {data.Length} is less than expected {_layout.TotalSize}");
		}

		return Result.Ok(new PlcExecutionInfo(
			RecipeActive: data[_layout.RecipeActiveOffset] != 0 ||
						  data[_layout.RecipeActiveOffset + 1] != 0,
			ActualLine: BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(_layout.ActualLineOffset)),
			StepCurrentTime: BitConverter.Int32BitsToSingle(
				BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(_layout.StepCurrentTimeOffset))),
			ForLoopCount1: BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(_layout.ForLoopCount1Offset)),
			ForLoopCount2: BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(_layout.ForLoopCount2Offset)),
			ForLoopCount3: BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(_layout.ForLoopCount3Offset))));
	}
}
