using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "DI")]
[Trait("Category", "Unit")]
public sealed class S7DiTransportSingletonTests
{
	[Fact]
	public async Task TransportConsumers_ResolvedFromContainer_ShareOneS7DriverInstance()
	{
		var appConfiguration = BuildAppConfiguration();

		var services = new ServiceCollection();
		services.AddSingleton(appConfiguration);
		services.AddSingleton(appConfiguration.PlcConfiguration);
		services.AddSingleton(new RecipeMetadataRegistry(appConfiguration));
		services.AddS7();

		await using var provider = services.BuildServiceProvider();

		var driver = provider.GetRequiredService<S7Driver>();
		var transport = provider.GetRequiredService<IS7Transport>();

		transport.Should().BeSameAs(driver);
	}

	private static AppConfiguration BuildAppConfiguration()
	{
		var stringProperty = TestPropertyTypeDefinitionBuilder.CreateString("comment", 24);
		var propertyMap = new Dictionary<string, PropertyTypeDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			[stringProperty.Id] = stringProperty
		};

		return new AppConfiguration(
			Properties: propertyMap,
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: AppUiOptions.Default);
	}
}
