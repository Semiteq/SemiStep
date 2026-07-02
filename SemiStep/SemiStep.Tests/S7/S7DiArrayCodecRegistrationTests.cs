using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "DI")]
[Trait("Category", "Integration")]
public sealed class S7DiArrayCodecRegistrationTests
{
	[Fact]
	public void ArrayCodec_ResolvedFromContainer_ConsultsRegistryForWStringMaxChars()
	{
		const int ConfiguredMaxLength = 24;

		var appConfiguration = BuildAppConfiguration(new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateString("comment", ConfiguredMaxLength)
		});

		var services = new ServiceCollection();
		services.AddSingleton(appConfiguration);
		services.AddSingleton(appConfiguration.PlcConfiguration);
		services.AddSingleton(new RecipeMetadataRegistry(appConfiguration));
		services.AddS7();

		using var provider = services.BuildServiceProvider();
		var codec = provider.GetRequiredService<ArrayCodec>();

		var expectedElementSize = 4 + ConfiguredMaxLength * 2;
		codec.WStringElementSize.Should().Be(expectedElementSize);
	}

	[Fact]
	public void RegistryConstruction_WithoutStringProperty_FailsFast()
	{
		var appConfiguration = BuildAppConfiguration(new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateFloat("temperature")
		});

		var action = () => new RecipeMetadataRegistry(appConfiguration);

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*string*");
	}

	private static AppConfiguration BuildAppConfiguration(IEnumerable<PropertyTypeDefinition> properties)
	{
		var propertyMap = new Dictionary<string, PropertyTypeDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var property in properties)
		{
			propertyMap[property.Id] = property;
		}

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
