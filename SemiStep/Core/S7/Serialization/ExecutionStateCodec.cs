using System.Buffers.Binary;

using FluentResults;

using TypesShared.Plc;
using TypesShared.Plc.Memory;

namespace S7.Serialization;

internal sealed class ExecutionStateCodec
{
	private readonly ExecutionDbLayout _layout;

	public ExecutionStateCodec(ExecutionDbLayout layout)
	{
		if (layout.TotalSize < layout.RecipeActiveOffset + 2)
		{
			throw new ArgumentException(
				$"ExecutionDbLayout.TotalSize ({layout.TotalSize}) must be at least " +
				$"RecipeActiveOffset ({layout.RecipeActiveOffset}) + 2 bytes",
				nameof(layout));
		}

		if (layout.TotalSize < layout.ActualLineOffset + sizeof(int))
		{
			throw new ArgumentException(
				$"ExecutionDbLayout.TotalSize ({layout.TotalSize}) must be at least " +
				$"ActualLineOffset ({layout.ActualLineOffset}) + 4 bytes",
				nameof(layout));
		}

		if (layout.TotalSize < layout.StepCurrentTimeOffset + sizeof(int))
		{
			throw new ArgumentException(
				$"ExecutionDbLayout.TotalSize ({layout.TotalSize}) must be at least " +
				$"StepCurrentTimeOffset ({layout.StepCurrentTimeOffset}) + 4 bytes",
				nameof(layout));
		}

		if (layout.TotalSize < layout.ForLoopCount1Offset + sizeof(int))
		{
			throw new ArgumentException(
				$"ExecutionDbLayout.TotalSize ({layout.TotalSize}) must be at least " +
				$"ForLoopCount1Offset ({layout.ForLoopCount1Offset}) + 4 bytes",
				nameof(layout));
		}

		if (layout.TotalSize < layout.ForLoopCount2Offset + sizeof(int))
		{
			throw new ArgumentException(
				$"ExecutionDbLayout.TotalSize ({layout.TotalSize}) must be at least " +
				$"ForLoopCount2Offset ({layout.ForLoopCount2Offset}) + 4 bytes",
				nameof(layout));
		}

		if (layout.TotalSize < layout.ForLoopCount3Offset + sizeof(int))
		{
			throw new ArgumentException(
				$"ExecutionDbLayout.TotalSize ({layout.TotalSize}) must be at least " +
				$"ForLoopCount3Offset ({layout.ForLoopCount3Offset}) + 4 bytes",
				nameof(layout));
		}

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
