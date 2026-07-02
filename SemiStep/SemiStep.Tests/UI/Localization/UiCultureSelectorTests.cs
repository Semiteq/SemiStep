using FluentAssertions;

using SemiStep.UI.Localization;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class UiCultureSelectorTests
{
	[Fact]
	public void Resolve_EnglishTag_ReturnsEnglish()
	{
		UiCultureSelector.Resolve("en").Name.Should().Be("en");
	}

	[Fact]
	public void Resolve_RussianTag_ReturnsRussian()
	{
		UiCultureSelector.Resolve("ru").Name.Should().Be("ru");
	}

	[Fact]
	public void Resolve_Null_ReturnsRussian()
	{
		UiCultureSelector.Resolve(null).Name.Should().Be("ru");
	}

	[Theory]
	[InlineData("")]
	[InlineData("  ")]
	public void Resolve_Blank_ReturnsRussian(string locale)
	{
		UiCultureSelector.Resolve(locale).Name.Should().Be("ru");
	}

	[Fact]
	public void Resolve_InvalidTag_ReturnsRussian()
	{
		UiCultureSelector.Resolve("zz-ZZ-garbage").Name.Should().Be("ru");
	}
}
