using System.Buffers.Binary;

using FluentAssertions;

using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Serialization;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "ArrayCodec")]
[Trait("Category", "Unit")]
public sealed class ArrayCodecWStringMaxCharsTests
{
	private static ArrayCodec BuildCodec(int wStringMaxChars)
	{
		return new ArrayCodec(
			DataDbLayout.DefaultInt,
			DataDbLayout.DefaultFloat,
			DataDbLayout.DefaultString,
			wStringMaxChars);
	}

	[Theory]
	[InlineData(16)]
	[InlineData(32)]
	public void WStringElementSize_EqualsHeaderPlusTwoBytesPerChar(int wStringMaxChars)
	{
		var codec = BuildCodec(wStringMaxChars);

		codec.WStringElementSize.Should().Be(4 + wStringMaxChars * 2);
	}

	[Theory]
	[InlineData(16)]
	[InlineData(32)]
	public void EncodeThenDecode_StringArray_RoundTripsValues(int wStringMaxChars)
	{
		var codec = BuildCodec(wStringMaxChars);
		var values = new[] { "abc", "x", new string('q', wStringMaxChars) };

		var bytes = codec.EncodeStringArray(values);
		var decoded = codec.DecodeStringArray(bytes, values.Length);

		decoded.Should().Equal(values);

		// Header sanity: the first slot's maxLength field must equal the codec's configured
		// max chars, big-endian. Guards against a future drift where Encode silently writes 0.
		var firstSlotOffset = DataDbLayout.DefaultString.DataStartOffset;
		var headerMaxLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(firstSlotOffset));
		headerMaxLength.Should().Be((ushort)wStringMaxChars);
	}

	[Fact]
	public void EncodeThenDecode_EmptyString_RoundTripsAsEmpty()
	{
		var codec = BuildCodec(wStringMaxChars: 16);

		var bytes = codec.EncodeStringArray(new[] { "" });
		var decoded = codec.DecodeStringArray(bytes, 1);

		decoded.Should().Equal(new[] { "" });
	}

	[Fact]
	public void EncodeStringArray_EmbeddedNulCharacter_ThrowsArgumentException()
	{
		var codec = BuildCodec(wStringMaxChars: 16);

		var action = () => codec.EncodeStringArray(new[] { "ab\0c" });

		action.Should().Throw<ArgumentException>()
			.WithMessage("*NUL*");
	}

	[Theory]
	[InlineData(16)]
	[InlineData(32)]
	public void EncodeStringArray_ProducesExpectedBufferSize(int wStringMaxChars)
	{
		var codec = BuildCodec(wStringMaxChars);
		var values = new[] { "a", "b", "c" };

		var bytes = codec.EncodeStringArray(values);

		var expectedSize = DataDbLayout.DefaultString.DataStartOffset
			+ values.Length * codec.WStringElementSize;
		bytes.Length.Should().Be(expectedSize);
	}

	[Fact]
	public void EncodeStringArray_OverLengthValue_ThrowsArgumentException()
	{
		var codec = BuildCodec(wStringMaxChars: 16);
		var overLength = new string('z', 17);

		var action = () => codec.EncodeStringArray(new[] { overLength });

		action.Should().Throw<ArgumentException>()
			.WithMessage("*17*16*");
	}

	[Fact]
	public void DecodeStringArray_PlcSlotActualLengthOverMaxChars_ThrowsInvalidDataException()
	{
		// Craft a byte buffer whose WString header advertises an actualLength greater than the
		// configured max chars. ReadWString must hard-fail to mirror WriteWString's contract,
		// instead of silently truncating like the legacy behaviour.
		const int WStringMaxChars = 16;
		var codec = BuildCodec(WStringMaxChars);
		var layout = DataDbLayout.DefaultString;
		var elementSize = codec.WStringElementSize;
		var buffer = new byte[layout.DataStartOffset + elementSize];

		BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(layout.CapacityOffset), 1u);
		BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(layout.CurrentSizeOffset), 1u);

		var slotOffset = layout.DataStartOffset;
		BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(slotOffset), (ushort)WStringMaxChars);
		BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(slotOffset + 2), (ushort)(WStringMaxChars + 1));

		var action = () => codec.DecodeStringArray(buffer, 1);

		action.Should().Throw<InvalidDataException>()
			.WithMessage($"*{WStringMaxChars + 1}*{WStringMaxChars}*");
	}

}
