using System.Buffers.Binary;

using FluentResults;

using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.State;

namespace SemiStep.Core.Plc.S7.Serialization;

internal sealed class ManagingAreaCodec(ManagingDbLayout layout)
{
	private readonly ManagingDbLayout _layout = layout;

	public Result<PlcManagingAreaState> Decode(byte[] data)
	{
		if (data.Length < _layout.TotalSize)
		{
			return Result.Fail(
				$"Data length {data.Length} is less than expected {_layout.TotalSize}");
		}

		var committed = data[_layout.CommittedOffset] != 0;
		var recipeLines = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(_layout.RecipeLinesOffset));

		return Result.Ok(new PlcManagingAreaState(committed, recipeLines));
	}

	public byte[] EncodePcData(ManagingAreaPcData data)
	{
		var bytes = new byte[_layout.TotalSize];

		bytes[_layout.CommittedOffset] = data.Committed ? (byte)0x01 : (byte)0x00;
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(_layout.RecipeLinesOffset), data.RecipeLines);

		return bytes;
	}
}
