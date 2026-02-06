using System.IO.Hashing;

namespace S7.Protocol;

/// <summary>
/// CRC-32/ISO-HDLC checksum calculator using standard .NET implementation.
/// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.io.hashing.crc32
/// </summary>
public static class ChecksumCalculator
{
	public static uint ComputeCrc32(byte[] data)
	{
		return Crc32.HashToUInt32(data);
	}

	public static uint ComputeCrc32(int[] values)
	{
		var bytes = new byte[values.Length * sizeof(int)];
		Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
		return Crc32.HashToUInt32(bytes);
	}

	public static uint ComputeCrc32(float[] values)
	{
		var bytes = new byte[values.Length * sizeof(float)];
		Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
		return Crc32.HashToUInt32(bytes);
	}

	public static uint ComputeStringArrayCrc32(string[] values, int maxLength)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);

		foreach (var str in values)
		{
			var truncated = str.Length > maxLength ? str[..maxLength] : str;
			foreach (var ch in truncated)
			{
				writer.Write((ushort)ch);
			}
			for (var i = truncated.Length; i < maxLength; i++)
			{
				writer.Write((ushort)0);
			}
		}

		return Crc32.HashToUInt32(stream.ToArray());
	}
}
