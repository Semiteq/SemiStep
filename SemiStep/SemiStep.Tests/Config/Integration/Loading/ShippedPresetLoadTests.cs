using AwesomeAssertions;

using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loading;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "Loading")]
public sealed class ShippedPresetLoadTests
{
	[Theory]
	[InlineData("MBE")]
	[InlineData("MOCVD")]
	[InlineData("RIE")]
	public async Task ShippedPreset_LoadsThroughFacade(string equipmentName)
	{
		var configuration = await ShippedConfigLocator.LoadAsync(equipmentName);

		configuration.Ui.Locale.Should().Be("ru");
	}
}
