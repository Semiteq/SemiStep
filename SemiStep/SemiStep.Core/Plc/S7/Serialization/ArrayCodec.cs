using System.Buffers.Binary;
using System.Text;

using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Protocol;

namespace SemiStep.Core.Plc.S7.Serialization;

internal sealed class ArrayCodec
{
	private readonly DataDbLayout _intLayout;
	private readonly DataDbLayout _floatLayout;
	private readonly DataDbLayout _stringLayout;
	private readonly int _wStringMaxChars;

	public ArrayCodec(
		DataDbLayout intLayout,
		DataDbLayout floatLayout,
		DataDbLayout stringLayout,
		int wStringMaxChars)
	{
		_intLayout = intLayout;
		_floatLayout = floatLayout;
		_stringLayout = stringLayout;
		_wStringMaxChars = wStringMaxChars;
	}

	public int WStringElementSize => ProtocolConstants.WStringHeaderSize + _wStringMaxChars * 2;

	public int[] DecodeIntArray(byte[] data, int count)
	{
		var startOffset = _intLayout.DataStartOffset;
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
		var startOffset = _floatLayout.DataStartOffset;
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
		var startOffset = _stringLayout.DataStartOffset;
		var result = new string[count];

		for (var i = 0; i < count; i++)
		{
			var offset = startOffset + i * WStringElementSize;
			result[i] = ReadWString(data, offset);
		}

		return result;
	}

	public byte[] EncodeIntArray(int[] values)
	{
		var dataSize = _intLayout.DataStartOffset + values.Length * ProtocolConstants.IntElementSize;
		var bytes = new byte[dataSize];

		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(_intLayout.CapacityOffset), (uint)values.Length);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(_intLayout.CurrentSizeOffset), (uint)values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			var offset = _intLayout.DataStartOffset + i * ProtocolConstants.IntElementSize;
			BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), values[i]);
		}

		return bytes;
	}

	public byte[] EncodeFloatArray(float[] values)
	{
		var dataSize = _floatLayout.DataStartOffset + values.Length * ProtocolConstants.FloatElementSize;
		var bytes = new byte[dataSize];

		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(_floatLayout.CapacityOffset), (uint)values.Length);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(_floatLayout.CurrentSizeOffset), (uint)values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			var offset = _floatLayout.DataStartOffset + i * ProtocolConstants.FloatElementSize;
			var intBits = BitConverter.SingleToInt32Bits(values[i]);
			BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), intBits);
		}

		return bytes;
	}

	public byte[] EncodeStringArray(string[] values)
	{
		var dataSize = _stringLayout.DataStartOffset + values.Length * WStringElementSize;
		var bytes = new byte[dataSize];

		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(_stringLayout.CapacityOffset), (uint)values.Length);
		BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(_stringLayout.CurrentSizeOffset), (uint)values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			var offset = _stringLayout.DataStartOffset + i * WStringElementSize;
			WriteWString(bytes, offset, values[i]);
		}

		return bytes;
	}

	public static int ReadArrayCurrentSize(byte[] headerData, DataDbLayout layout)
	{
		return (int)BinaryPrimitives.ReadUInt32BigEndian(headerData.AsSpan(layout.CurrentSizeOffset));
	}

	private string ReadWString(byte[] data, int offset)
	{
		// Header capacity field is informational; codec sizing is driven by _wStringMaxChars.
		_ = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
		var actualLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2));

		if (actualLength > _wStringMaxChars)
		{
			throw new InvalidDataException(
				$"PLC WString actual length {actualLength} exceeds configured max chars {_wStringMaxChars}");
		}

		var charCount = (int)actualLength;

		var sb = new StringBuilder(charCount);
		for (var i = 0; i < charCount; i++)
		{
			var charOffset = offset + ProtocolConstants.WStringHeaderSize + i * 2;
			var ch = (char)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(charOffset));
			sb.Append(ch);
		}

		return sb.ToString();
	}

	private void WriteWString(byte[] data, int offset, string value)
	{
		if (value.Length > _wStringMaxChars)
		{
			throw new ArgumentException(
				$"String length {value.Length} exceeds WString max chars {_wStringMaxChars}",
				nameof(value));
		}

		if (value.Contains('\0'))
		{
			throw new ArgumentException(
				"WString values must not contain embedded NUL characters",
				nameof(value));
		}

		BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset), (ushort)_wStringMaxChars);
		BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset + 2), (ushort)value.Length);

		for (var i = 0; i < value.Length; i++)
		{
			var charOffset = offset + ProtocolConstants.WStringHeaderSize + i * 2;
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(charOffset), value[i]);
		}
	}
}
