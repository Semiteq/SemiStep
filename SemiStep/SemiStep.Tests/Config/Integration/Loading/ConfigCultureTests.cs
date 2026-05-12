using FluentAssertions;

using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loading;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "ConfigCulture")]
public sealed class ConfigCultureTests
{
	[Fact]
	public async Task LoadStandard_UnderRuRuCulture_Succeeds()
	{
		using var _ = new CultureScope("ru-RU");

		var config = await ConfigTestHelper.LoadValidCaseAsync();

		config.Should().NotBeNull();
		config.Actions.Should().NotBeEmpty("Standard config should load under ru-RU culture");
		config.Properties.Should().NotBeEmpty();
		config.Columns.Should().NotBeEmpty();
	}
}
