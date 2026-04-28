using System.Buffers.Binary;
using System.Text;

using S7.Protocol;

using TypesShared.Plc.Memory;

namespace S7.Serialization;

internal sealed class ArrayCodec(DataDbLayout intLayout, DataDbLayout floatLayout, DataDbLayout stringLayout)
{
	public int[] DecodeIntArray(byte[] data, int count)
	{
		var startOffset = intLayout.DataStartOffset;
		var result = new int[count];

		for (var i = 0; i < count; i++)
		{
			var offset = startOffset + i * ProtocolConstants.IntElementSize;
			result[i] = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
		}

		return result;
	}

	public float[] DecodeFloatArray(byte[] data, int count)
	{
		var startOffset = floatLayout.DataStartOffset;
		var result = new float[count];

		for (var i = 0; i < count; i++)
		{
			var offset = startOffset + i * ProtocolConstants.FloatElementSize;
			var intBits = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
			result[i] = BitConverter.Int32BitsToSingle(intBits);
		}

		return result;
	}

	public string[] DecodeStringArray(byte[] data, int count)
	{
		var startOffset = stringLayout.DataStartOffset;
		var result = new string[count];

		for (var i = 0; i < count; i++)
		{
			var offset = startOffset + i * ProtocolConstants.WStringElementSize;
			result[i] = ReadWString(data, offset);
		}

		return result;
	}

	public byte[] EncodeIntArray(int[] values)
	{
		var dataSize = intLayout.DataStartOffset + values.Length * ProtocolConstants.IntElementSize;
		var bytes = new byte[dataSize];

		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(intLayout.CapacityOffset), (uint)values.Length);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(intLayout.CurrentSizeOffset), (uint)values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			var offset = intLayout.DataStartOffset + i * ProtocolConstants.IntElementSize;
			BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), values[i]);
		}

		return bytes;
	}

	public byte[] EncodeFloatArray(float[] values)
	{
		var dataSize = floatLayout.DataStartOffset + values.Length * ProtocolConstants.FloatElementSize;
		var bytes = new byte[dataSize];

		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(floatLayout.CapacityOffset), (uint)values.Length);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(floatLayout.CurrentSizeOffset), (uint)values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			var offset = floatLayout.DataStartOffset + i * ProtocolConstants.FloatElementSize;
			var intBits = BitConverter.SingleToInt32Bits(values[i]);
			BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), intBits);
		}

		return bytes;
	}

	public byte[] EncodeStringArray(string[] values)
	{
		var dataSize = stringLayout.DataStartOffset + values.Length * ProtocolConstants.WStringElementSize;
		var bytes = new byte[dataSize];

		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(stringLayout.CapacityOffset), (uint)values.Length);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(stringLayout.CurrentSizeOffset), (uint)values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			var offset = stringLayout.DataStartOffset + i * ProtocolConstants.WStringElementSize;
			WriteWString(bytes, offset, values[i]);
		}

		return bytes;
	}

	public static int ReadArrayCurrentSize(byte[] headerData, DataDbLayout layout)
	{
		return (int)BinaryPrimitives.ReadUInt32BigEndian(headerData.AsSpan(layout.CurrentSizeOffset));
	}

	private static string ReadWString(byte[] data, int offset)
	{
		var maxLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
		var actualLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2));

		var charCount = Math.Min((int)actualLength, (int)maxLength);
		var charCount2 = Math.Min(charCount, ProtocolConstants.WStringMaxChars);

		var sb = new StringBuilder(charCount2);
		for (var i = 0; i < charCount2; i++)
		{
			var charOffset = offset + ProtocolConstants.WStringHeaderSize + i * 2;
			var ch = (char)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(charOffset));
			if (ch == '\0')
			{
				break;
			}
			sb.Append(ch);
		}

		return sb.ToString();
	}

	private static void WriteWString(byte[] data, int offset, string value)
	{
		var truncated = value.Length > ProtocolConstants.WStringMaxChars
			? value[..ProtocolConstants.WStringMaxChars]
			: value;

		BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset), (ushort)ProtocolConstants.WStringMaxChars);
		BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset + 2), (ushort)truncated.Length);

		for (var i = 0; i < truncated.Length; i++)
		{
			var charOffset = offset + ProtocolConstants.WStringHeaderSize + i * 2;
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(charOffset), truncated[i]);
		}
	}
}
