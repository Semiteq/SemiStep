using System.Globalization;

using FluentAssertions;

using SemiStep.UI.Localization;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class ResourceResolutionTests
{
	[Fact]
	public void GetString_EnglishCulture_ReturnsNeutralValue()
	{
		Resources.ResourceManager.GetString("MenuFile", new CultureInfo("en"))
			.Should().Be("_File");
	}

	[Fact]
	public void GetString_RussianCulture_ReturnsSatelliteValue()
	{
		Resources.ResourceManager.GetString("MenuFile", new CultureInfo("ru"))
			.Should().Be("_Файл");
	}

	[Fact]
	public void Culture_Russian_ResolvesTypedAccessorToSatelliteValue()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			Resources.MenuFile.Should().Be("_Файл");
		}
	}
}
