using System.Buffers.Binary;
using System.Text;

using S7.Protocol;

namespace S7.Serialization;

internal static class ArrayCodec
{
	public static int[] DecodeIntArray(byte[] data, int count)
	{
		var startOffset = DataArrayLayout.DataStartOffset;
		var result = new int[count];

		for (var i = 0; i < count; i++)
		{
			var offset = startOffset + i * DataArrayLayout.IntElementSize;
			result[i] = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
		}

		return result;
	}

	public static float[] DecodeFloatArray(byte[] data, int count)
	{
		var startOffset = DataArrayLayout.DataStartOffset;
		var result = new float[count];

		for (var i = 0; i < count; i++)
		{
			var offset = startOffset + i * DataArrayLayout.FloatElementSize;
			var intBits = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
			result[i] = BitConverter.Int32BitsToSingle(intBits);
		}

		return result;
	}

	public static string[] DecodeStringArray(byte[] data, int count)
	{
		var startOffset = DataArrayLayout.DataStartOffset;
		var result = new string[count];

		for (var i = 0; i < count; i++)
		{
			var offset = startOffset + i * DataArrayLayout.WStringElementSize;
			result[i] = ReadWString(data, offset);
		}

		return result;
	}

	public static byte[] EncodeIntArray(int[] values)
	{
		var dataSize = DataArrayLayout.DataStartOffset + values.Length * DataArrayLayout.IntElementSize;
		var bytes = new byte[dataSize];

		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(DataArrayLayout.CapacityOffset), (uint)values.Length);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(DataArrayLayout.CurrentSizeOffset), (uint)values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			var offset = DataArrayLayout.DataStartOffset + i * DataArrayLayout.IntElementSize;
			BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), values[i]);
		}

		return bytes;
	}

	public static byte[] EncodeFloatArray(float[] values)
	{
		var dataSize = DataArrayLayout.DataStartOffset + values.Length * DataArrayLayout.FloatElementSize;
		var bytes = new byte[dataSize];

		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(DataArrayLayout.CapacityOffset), (uint)values.Length);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(DataArrayLayout.CurrentSizeOffset), (uint)values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			var offset = DataArrayLayout.DataStartOffset + i * DataArrayLayout.FloatElementSize;
			var intBits = BitConverter.SingleToInt32Bits(values[i]);
			BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), intBits);
		}

		return bytes;
	}

	public static byte[] EncodeStringArray(string[] values)
	{
		var dataSize = DataArrayLayout.DataStartOffset + values.Length * DataArrayLayout.WStringElementSize;
		var bytes = new byte[dataSize];

		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(DataArrayLayout.CapacityOffset), (uint)values.Length);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(DataArrayLayout.CurrentSizeOffset), (uint)values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			var offset = DataArrayLayout.DataStartOffset + i * DataArrayLayout.WStringElementSize;
			WriteWString(bytes, offset, values[i]);
		}

		return bytes;
	}

	public static int ReadArrayCurrentSize(byte[] headerData)
	{
		return (int)BinaryPrimitives.ReadUInt32BigEndian(headerData.AsSpan(DataArrayLayout.CurrentSizeOffset));
	}

	private static string ReadWString(byte[] data, int offset)
	{
		var maxLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
		var actualLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2));

		var charCount = Math.Min((int)actualLength, (int)maxLength);
		var charCount2 = Math.Min(charCount, DataArrayLayout.WStringMaxChars);

		var sb = new StringBuilder(charCount2);
		for (var i = 0; i < charCount2; i++)
		{
			var charOffset = offset + DataArrayLayout.WStringHeaderSize + i * 2;
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
		var truncated = value.Length > DataArrayLayout.WStringMaxChars
			? value[..DataArrayLayout.WStringMaxChars]
			: value;

		BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset), (ushort)DataArrayLayout.WStringMaxChars);
		BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset + 2), (ushort)truncated.Length);

		for (var i = 0; i < truncated.Length; i++)
		{
			var charOffset = offset + DataArrayLayout.WStringHeaderSize + i * 2;
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(charOffset), truncated[i]);
		}
	}
}
