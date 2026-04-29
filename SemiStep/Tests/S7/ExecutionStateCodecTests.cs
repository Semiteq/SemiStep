using System.Buffers.Binary;

using FluentAssertions;

using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Serialization;

using Xunit;

namespace Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "Serialization")]
[Trait("Category", "Unit")]
public sealed class ExecutionStateCodecTests
{
	private static ExecutionDbLayout DefaultLayout => ExecutionDbLayout.Default;

	private static ExecutionStateCodec BuildCodec()
	{
		return new ExecutionStateCodec(DefaultLayout);
	}

	[Fact]
	public void Decode_TooShortData_ReturnsFailedResult()
	{
		var codec = BuildCodec();
		var shortBytes = new byte[DefaultLayout.TotalSize - 1];

		var result = codec.Decode(shortBytes);

		result.IsFailed.Should().BeTrue();
		result.Errors[0].Message.Should().Contain("length");
	}

	[Fact]
	public void Decode_RecipeActive_TrueWhenFirstByteNonZero()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];
		bytes[DefaultLayout.RecipeActiveOffset] = 0x01;
		bytes[DefaultLayout.RecipeActiveOffset + 1] = 0x00;

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.RecipeActive.Should().BeTrue();
	}

	[Fact]
	public void Decode_RecipeActive_TrueWhenSecondByteNonZero()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];
		bytes[DefaultLayout.RecipeActiveOffset] = 0x00;
		bytes[DefaultLayout.RecipeActiveOffset + 1] = 0x01;

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.RecipeActive.Should().BeTrue();
	}

	[Fact]
	public void Decode_RecipeActive_FalseWhenBothBytesZero()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];
		bytes[DefaultLayout.RecipeActiveOffset] = 0x00;
		bytes[DefaultLayout.RecipeActiveOffset + 1] = 0x00;

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.RecipeActive.Should().BeFalse();
	}

	[Fact]
	public void Decode_ActualLine_ParsedBigEndianFromOffset()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];
		const int ExpectedLine = 42;
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(DefaultLayout.ActualLineOffset), ExpectedLine);

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.ActualLine.Should().Be(ExpectedLine);
	}

	[Fact]
	public void Decode_StepCurrentTime_ParsedAsIeee754BigEndian()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];
		const float ExpectedTime = 3.14f;
		BinaryPrimitives.WriteInt32BigEndian(
			bytes.AsSpan(DefaultLayout.StepCurrentTimeOffset),
			BitConverter.SingleToInt32Bits(ExpectedTime));

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.StepCurrentTime.Should().Be(ExpectedTime);
	}

	[Fact]
	public void Decode_ForLoopCounts_ParsedBigEndianFromOffsets()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];
		const int Count1 = 10;
		const int Count2 = 20;
		const int Count3 = 30;
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(DefaultLayout.ForLoopCount1Offset), Count1);
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(DefaultLayout.ForLoopCount2Offset), Count2);
		BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(DefaultLayout.ForLoopCount3Offset), Count3);

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.ForLoopCount1.Should().Be(Count1);
		result.Value.ForLoopCount2.Should().Be(Count2);
		result.Value.ForLoopCount3.Should().Be(Count3);
	}

	[Fact]
	public void Decode_AllZeroBytes_ReturnsInactiveRecipeAndZeroFields()
	{
		var codec = BuildCodec();
		var bytes = new byte[DefaultLayout.TotalSize];

		var result = codec.Decode(bytes);

		result.IsSuccess.Should().BeTrue();
		result.Value.RecipeActive.Should().BeFalse();
		result.Value.ActualLine.Should().Be(0);
		result.Value.StepCurrentTime.Should().Be(0f);
		result.Value.ForLoopCount1.Should().Be(0);
		result.Value.ForLoopCount2.Should().Be(0);
		result.Value.ForLoopCount3.Should().Be(0);
	}

	[Fact]
	public void Constructor_WithInvalidLayout_DoesNotThrow_ValidationIsConfigFacadeResponsibility()
	{
		// The codec must not perform layout validation — that responsibility belongs
		// to ConfigFacade via PlcConfigurationValidator. The codec accepts any layout.
		var malformedLayout = new ExecutionDbLayout(
			DbNumber: 0,
			RecipeActiveOffset: 0,
			ActualLineOffset: 0,
			StepCurrentTimeOffset: 0,
			ForLoopCount1Offset: 0,
			ForLoopCount2Offset: 0,
			ForLoopCount3Offset: 0,
			TotalSize: 0);

		var act = () => new ExecutionStateCodec(malformedLayout);

		act.Should().NotThrow();
	}
}
