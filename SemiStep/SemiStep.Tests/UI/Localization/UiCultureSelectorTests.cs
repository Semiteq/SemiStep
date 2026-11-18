using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.UI.Localization;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class UiCultureSelectorTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	[InlineData("zz-ZZ-garbage")]
	public void Resolve_MissingOrInvalidTag_ReturnsDefaultLocale(string? locale)
	{
		UiCultureSelector.Resolve(locale).Name.Should().Be(AppUiOptions.DefaultLocale);
	}
}
