using FluentAssertions;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Category", "Unit")]
public sealed class PropertyTimeMultiConverterTests
{
	[Theory]
	[InlineData(1.1952192f, "1.195")]
	[InlineData(1.2f, "1.2")]
	[InlineData(5f, "5")]
	[InlineData(0.12345f, "0.123")]
	public void FormatNumeric_Float_BoundsToThreeInvariantDecimals(float value, string expected)
	{
		PropertyTimeMultiConverter.FormatNumeric(value).Should().Be(expected);
	}

	[Fact]
	public void FormatNumeric_Int_IsUnchanged()
	{
		PropertyTimeMultiConverter.FormatNumeric(20).Should().Be("20");
	}

	[Fact]
	public void FormatNumeric_String_PassesThrough()
	{
		PropertyTimeMultiConverter.FormatNumeric("n/a").Should().Be("n/a");
	}
}
