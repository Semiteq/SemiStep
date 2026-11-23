using System.Buffers.Binary;

using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.State;

namespace SemiStep.Tools.PlcEmulator;

internal sealed class ExecutionAreaEncoder(ExecutionDbLayout layout)
{
	public byte[] Encode(PlcExecutionInfo info)
	{
		var bytes = new byte[layout.TotalSize];

		bytes[layout.RecipeActiveOffset] = info.RecipeActive ? (byte)1 : (byte)0;
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(layout.ActualLineOffset), info.ActualLine);
		BinaryPrimitives.WriteInt32BigEndian(
			bytes.AsSpan(layout.StepCurrentTimeOffset),
			BitConverter.SingleToInt32Bits(info.StepCurrentTime));
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(layout.ForLoopCount1Offset), info.ForLoopCount1);
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(layout.ForLoopCount2Offset), info.ForLoopCount2);
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(layout.ForLoopCount3Offset), info.ForLoopCount3);

		return bytes;
	}
}
