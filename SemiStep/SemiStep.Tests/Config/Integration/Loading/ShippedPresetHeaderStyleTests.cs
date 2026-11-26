using AwesomeAssertions;

using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loading;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "GridHeader")]
public sealed class ShippedPresetHeaderStyleTests
{
	[Theory]
	[InlineData("MBE")]
	[InlineData("MOCVD")]
	[InlineData("RIE")]
	public async Task ShippedPreset_Header_Is12SemiBoldGrey(string equipmentName)
	{
		var configuration = await ShippedConfigLocator.LoadAsync(equipmentName);

		configuration.GridStyle.Fonts.HeaderFontSize.Should().Be(12);
		configuration.GridStyle.Fonts.HeaderFontWeight.Should().Be(600);
		configuration.GridStyle.Chrome.HeaderForeground.ToString().Should().BeEquivalentTo("#6C707E");
	}
}
