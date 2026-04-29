using System.Buffers.Binary;

using FluentAssertions;

using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;

using Xunit;

namespace Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "Serialization")]
[Trait("Category", "Unit")]
public sealed class ManagingAreaCodecTests
{
	private static ManagingDbLayout DefaultLayout => ManagingDbLayout.Default;

	private static ManagingAreaCodec BuildCodec()
	{
		return new ManagingAreaCodec(DefaultLayout);
	}

	[Fact]
	public void EncodePcData_CommittedTrue_WritesByteOneAtOffset0()
	{
		var codec = BuildCodec();
		var data = new ManagingAreaPcData(Committed: true, RecipeLines: 0);

		var bytes = codec.EncodePcData(data);

		bytes[DefaultLayout.CommittedOffset].Should().Be(0x01);
	}

	[Fact]
	public void EncodePcData_CommittedFalse_WritesZeroAtOffset0()
	{
		var codec = BuildCodec();
		var data = new ManagingAreaPcData(Committed: false, RecipeLines: 0);

		var bytes = codec.EncodePcData(data);

		bytes[DefaultLayout.CommittedOffset].Should().Be(0x00);
	}

	[Fact]
	public void EncodePcData_Returns6ByteArray()
	{
		var codec = BuildCodec();
		var data = new ManagingAreaPcData(Committed: true, RecipeLines: 42);

		var bytes = codec.EncodePcData(data);

		bytes.Should().HaveCount(DefaultLayout.TotalSize);
	}

	[Fact]
	public void EncodePcData_RecipeLines_WrittenBigEndianAtOffset2()
	{
		var codec = BuildCodec();
		const int RecipeLines = 0x01020304;
		var data = new ManagingAreaPcData(Committed: false, RecipeLines: RecipeLines);

		var bytes = codec.EncodePcData(data);

		var decoded = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(DefaultLayout.RecipeLinesOffset));
		decoded.Should().Be(RecipeLines);
	}

	[Fact]
	public void EncodePcData_ZeroRecipeLines_Writes4ZeroBytes()
	{
		var codec = BuildCodec();
		var data = new ManagingAreaPcData(Committed: true, RecipeLines: 0);

		var bytes = codec.EncodePcData(data);

		bytes[DefaultLayout.RecipeLinesOffset].Should().Be(0);
		bytes[DefaultLayout.RecipeLinesOffset + 1].Should().Be(0);
		bytes[DefaultLayout.RecipeLinesOffset + 2].Should().Be(0);
		bytes[DefaultLayout.RecipeLinesOffset + 3].Should().Be(0);
	}

	[Fact]
	public void Decode_CommittedByte_NonZeroIsTrue()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];
		bytes[DefaultLayout.CommittedOffset] = 0x01;

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.Committed.Should().BeTrue();
	}

	[Fact]
	public void Decode_CommittedByte_ZeroIsFalse()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];
		bytes[DefaultLayout.CommittedOffset] = 0x00;

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.Committed.Should().BeFalse();
	}

	[Fact]
	public void Decode_RecipeLines_ParsedBigEndianFromOffset2()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];
		const int ExpectedLines = 99;
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(DefaultLayout.RecipeLinesOffset), ExpectedLines);

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.RecipeLines.Should().Be(ExpectedLines);
	}

	[Fact]
	public void Decode_AllZeroBytes_ReturnsCommittedFalseAndZeroLines()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.Committed.Should().BeFalse();
		result.Value.RecipeLines.Should().Be(0);
	}

	[Fact]
	public void Decode_TooShortData_ReturnsFailedResult()
	{
		var codec = BuildCodec();
		var shortBytes = new byte[3];

		var result = codec.Decode(shortBytes);

		result.IsFailed.Should().BeTrue();
		result.Errors[0].Message.Should().Contain("length");
	}

	[Fact]
	public void RoundTrip_CommittedTrueWithLines_PreservesValues()
	{
		var codec = BuildCodec();
		const int Lines = 17;
		var pcData = new ManagingAreaPcData(Committed: true, RecipeLines: Lines);

		var bytes = codec.EncodePcData(pcData);
		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.Committed.Should().BeTrue();
		result.Value.RecipeLines.Should().Be(Lines);
	}

	[Fact]
	public void RoundTrip_CommittedFalseWithLines_PreservesValues()
	{
		var codec = BuildCodec();
		const int Lines = 5;
		var pcData = new ManagingAreaPcData(Committed: false, RecipeLines: Lines);

		var bytes = codec.EncodePcData(pcData);
		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.Committed.Should().BeFalse();
		result.Value.RecipeLines.Should().Be(Lines);
	}

	[Fact]
	public void Constructor_WithInvalidLayout_DoesNotThrow_ValidationIsConfigFacadeResponsibility()
	{
		// The codec must not perform layout validation — that responsibility belongs
		// to ConfigFacade via PlcConfigurationValidator. The codec accepts any layout.
		var malformedLayout = new ManagingDbLayout(
			DbNumber: 0,
			CommittedOffset: 0,
			RecipeLinesOffset: 0,
			TotalSize: 0);

		var act = () => new ManagingAreaCodec(malformedLayout);

		act.Should().NotThrow();
	}
}
