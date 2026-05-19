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
	public void Constructor_NonPositiveMaxChars_ThrowsArgumentOutOfRangeException()
	{
		var action = () => new ArrayCodec(
			DataDbLayout.DefaultInt,
			DataDbLayout.DefaultFloat,
			DataDbLayout.DefaultString,
			wStringMaxChars: 0);

		action.Should().Throw<ArgumentOutOfRangeException>();
	}
}
