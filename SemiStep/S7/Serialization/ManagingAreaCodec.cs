using System.Buffers.Binary;

using Domain.Ports;

using S7.Protocol;

namespace S7.Serialization;

internal static class ManagingAreaCodec
{
	public static ManagingAreaState Decode(byte[] data)
	{
		if (data.Length < ManagingAreaLayout.TotalSize)
		{
			throw new ArgumentException(
				$"Data length {data.Length} is less than expected {ManagingAreaLayout.TotalSize}");
		}

		return new ManagingAreaState(
			PcStatus: (PcStatus)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(ManagingAreaLayout.PcStatusOffset)),
			PcTransactionId: BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ManagingAreaLayout.PcTransactionIdOffset)),
			PcChecksumInt: BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ManagingAreaLayout.PcChecksumIntOffset)),
			PcChecksumFloat: BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ManagingAreaLayout.PcChecksumFloatOffset)),
			PcChecksumString: BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ManagingAreaLayout.PcChecksumStringOffset)),
			PcRecipeLines: BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ManagingAreaLayout.PcRecipeLinesOffset)),
			PlcStatus: (PlcSyncStatus)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(ManagingAreaLayout.PlcStatusOffset)),
			PlcError: (PlcSyncError)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(ManagingAreaLayout.PlcErrorOffset)),
			PlcStoredId: BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ManagingAreaLayout.PlcStoredIdOffset)),
			PlcChecksumInt: BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ManagingAreaLayout.PlcChecksumIntOffset)),
			PlcChecksumFloat: BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ManagingAreaLayout.PlcChecksumFloatOffset)),
			PlcChecksumString: BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(ManagingAreaLayout.PlcChecksumStringOffset)));
	}

	public static byte[] EncodePcData(ManagingAreaPcData data)
	{
		var bytes = new byte[ManagingAreaLayout.PcDataSize];

		BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(ManagingAreaLayout.PcStatusOffset), (ushort)data.Status);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(ManagingAreaLayout.PcTransactionIdOffset), data.TransactionId);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(ManagingAreaLayout.PcChecksumIntOffset), data.ChecksumInt);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(ManagingAreaLayout.PcChecksumFloatOffset), data.ChecksumFloat);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(ManagingAreaLayout.PcChecksumStringOffset), data.ChecksumString);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(ManagingAreaLayout.PcRecipeLinesOffset), data.RecipeLines);

		return bytes;
	}
}
