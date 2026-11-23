using System.Buffers.Binary;
using System.Globalization;
using System.Text;

using FluentResults;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;

namespace SemiStep.Tools.PlcEmulator.Ui;

internal sealed class MemoryFieldCodec(PlcProtocolLayout layout, int stringMaxChars)
{
	private readonly ArrayCodec _arrayCodec = new(layout.IntDb, layout.FloatDb, layout.StringDb, stringMaxChars);

	public string Format(MemoryField field, ReadOnlySpan<byte> block)
	{
		return FormatValue(field.Type, block[field.Offset..]);
	}

	public string FormatValue(FieldType type, ReadOnlySpan<byte> bytes)
	{
		return type switch
		{
			FieldType.Bool => bytes[0] != 0 ? "TRUE" : "FALSE",
			FieldType.DInt => BinaryPrimitives.ReadInt32BigEndian(bytes).ToString(CultureInfo.InvariantCulture),
			FieldType.Real => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(bytes))
				.ToString("0.0##", CultureInfo.InvariantCulture),
			FieldType.WString => $"'{ReadWString(bytes)}'",
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unnamed field type")
		};
	}

	public Result<byte[]> Encode(MemoryField field, string text)
	{
		var trimmed = text.Trim();
		return field.Type switch
		{
			FieldType.Bool => EncodeBool(trimmed),
			FieldType.DInt => EncodeDInt(trimmed),
			FieldType.Real => EncodeReal(trimmed),
			FieldType.WString => EncodeWString(trimmed),
			_ => throw new ArgumentOutOfRangeException(nameof(field), field.Type, "Unnamed field type")
		};
	}

	private static Result<byte[]> EncodeBool(string text)
	{
		if (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1")
		{
			return Result.Ok<byte[]>([1]);
		}

		return text.Equals("false", StringComparison.OrdinalIgnoreCase) || text == "0"
			? Result.Ok<byte[]>([0])
			: Result.Fail("expected TRUE or FALSE");
	}

	private static Result<byte[]> EncodeDInt(string text)
	{
		if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
		{
			return Result.Fail("expected a whole number from -2147483648 to 2147483647");
		}

		var bytes = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32BigEndian(bytes, value);
		return Result.Ok(bytes);
	}

	private static Result<byte[]> EncodeReal(string text)
	{
		var normalized = text.Replace(',', '.');
		var parsed = float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
		if (!parsed || !float.IsFinite(value))
		{
			return Result.Fail("expected a number such as 12.5");
		}

		var bytes = new byte[sizeof(float)];
		BinaryPrimitives.WriteInt32BigEndian(bytes, BitConverter.SingleToInt32Bits(value));
		return Result.Ok(bytes);
	}

	private Result<byte[]> EncodeWString(string text)
	{
		var value = text.Length >= 2 && text[0] == '\'' && text[^1] == '\'' ? text[1..^1] : text;
		if (value.Length > stringMaxChars)
		{
			return Result.Fail($"expected at most {stringMaxChars} characters");
		}

		if (value.Contains('\0'))
		{
			return Result.Fail("expected text without NUL characters");
		}

		return Result.Ok(_arrayCodec.EncodeStringArray([value])[layout.StringDb.DataStartOffset..]);
	}

	private string ReadWString(ReadOnlySpan<byte> bytes)
	{
		var length = Math.Min(BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]), stringMaxChars);
		var builder = new StringBuilder(length);
		for (var i = 0; i < length; i++)
		{
			var charOffset = ProtocolConstants.WStringHeaderSize + (i * 2);
			builder.Append((char)BinaryPrimitives.ReadUInt16BigEndian(bytes[charOffset..]));
		}

		return builder.ToString();
	}
}
