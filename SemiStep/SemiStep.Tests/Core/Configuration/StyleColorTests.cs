using System;

using FluentAssertions;

using SemiStep.Core.Configuration;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Component", "Config")]
[Trait("Category", "Unit")]
public sealed class StyleColorTests
{
	[Fact]
	public void TryParse_SixDigit_SetsAlphaOpaque()
	{
		StyleColor.TryParse("#AABBCC", out var color).Should().BeTrue();

		color.A.Should().Be(0xFF);
		color.R.Should().Be(0xAA);
		color.G.Should().Be(0xBB);
		color.B.Should().Be(0xCC);
	}

	[Fact]
	public void TryParse_EightDigit_MapsArgbChannelOrder()
	{
		StyleColor.TryParse("#11223344", out var color).Should().BeTrue();

		color.A.Should().Be(0x11);
		color.R.Should().Be(0x22);
		color.G.Should().Be(0x33);
		color.B.Should().Be(0x44);
	}

	[Fact]
	public void TryParse_LowercaseInput_ParsesAndRoundTripsToUppercase()
	{
		StyleColor.TryParse("#aabbcc", out var color).Should().BeTrue();

		color.ToString().Should().Be("#AABBCC");
	}

	[Fact]
	public void ToString_OpaqueColor_EmitsSixDigitForm()
	{
		var color = new StyleColor(0xFF, 0x12, 0x34, 0x56);

		color.ToString().Should().Be("#123456");
	}

	[Fact]
	public void ToString_TranslucentColor_EmitsEightDigitForm()
	{
		var color = new StyleColor(0x80, 0x12, 0x34, 0x56);

		color.ToString().Should().Be("#80123456");
	}

	[Fact]
	public void Parse_TranslucentEightDigit_RoundTripsToUppercase()
	{
		StyleColor.Parse("#0aabbccd").ToString().Should().Be("#0AABBCCD");
	}

	[Fact]
	public void TryParse_MixedCase_ParsesAndRoundTripsToUppercase()
	{
		StyleColor.TryParse("#AaBbCc", out var color).Should().BeTrue();

		color.ToString().Should().Be("#AABBCC");
	}

	[Theory]
	[InlineData("#FFF")]
	[InlineData("#0FFF")]
	[InlineData("FFFFFF")]
	[InlineData("#12345")]
	[InlineData("#FFFFFFFFF")]
	[InlineData("#GGGGGG")]
	[InlineData("#1234ZZ")]
	[InlineData("#")]
	[InlineData("#1234567")]
	[InlineData("")]
	[InlineData("  ")]
	public void TryParse_RejectedInput_ReturnsFalse(string value)
	{
		StyleColor.TryParse(value, out var color).Should().BeFalse();

		color.Should().Be(default(StyleColor));
	}

	[Fact]
	public void TryParse_Null_ReturnsFalse()
	{
		StyleColor.TryParse(null, out var color).Should().BeFalse();

		color.Should().Be(default(StyleColor));
	}

	[Theory]
	[InlineData("#FFF")]
	[InlineData("#0FFF")]
	[InlineData("FFFFFF")]
	[InlineData("#12345")]
	[InlineData("#FFFFFFFFF")]
	[InlineData("#")]
	[InlineData("#1234567")]
	[InlineData("")]
	[InlineData("  ")]
	public void Parse_RejectedInput_ThrowsFormatExceptionNamingInput(string value)
	{
		var act = () => StyleColor.Parse(value);

		act.Should().Throw<FormatException>().WithMessage($"*{value}*");
	}

	[Fact]
	public void Parse_Null_ThrowsFormatException()
	{
		var act = () => StyleColor.Parse(null!);

		act.Should().Throw<FormatException>();
	}

	[Fact]
	public void Parse_EqualChannelsAcrossCase_AreValueEqual()
	{
		StyleColor.Parse("#aabbcc").Should().Be(StyleColor.Parse("#AABBCC"));
		(StyleColor.Parse("#aabbcc") == StyleColor.Parse("#AABBCC")).Should().BeTrue();
	}
}
