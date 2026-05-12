using FluentAssertions;

using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loading;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "ConfigNoise")]
public sealed class ConfigNoiseTests
{
	[Fact]
	public async Task LoadConfig_WithUnknownYamlFields_Succeeds()
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync("UnknownYamlFields");

		result.IsSuccess.Should().BeTrue(
			"unknown YAML fields should be silently ignored, not cause errors");
		result.Value.Actions.Should().NotBeEmpty();
		result.Value.Properties.Should().NotBeEmpty();
	}
}
