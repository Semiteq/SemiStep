using AwesomeAssertions;

using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loading;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "GridPalette")]
public sealed class ShippedPresetGridLineTests
{
	[Theory]
	[InlineData("MBE")]
	[InlineData("MOCVD")]
	[InlineData("RIE")]
	public async Task ShippedPreset_GridLine_IsDarkerThanTheGreyCells(string equipmentName)
	{
		var configuration = await ShippedConfigLocator.LoadAsync(equipmentName);

		configuration.GridStyle.Chrome.GridLine.ToString().Should().BeEquivalentTo("#B8B8B8");
	}
}
