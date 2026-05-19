using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Properties;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "RecipeMetadataRegistry")]
public sealed class RecipeMetadataRegistryStringMaxLengthTests
{
	[Fact]
	public void GetStringMaxLength_SingleStringProperty_ReturnsItsMaxLength()
	{
		var registry = BuildRegistry(
			Property("comment", "string", maxLength: 32));

		var result = registry.GetStringMaxLength();

		result.Should().Be(32);
	}

	[Fact]
	public void GetStringMaxLength_MultipleStringPropertiesWithSameMaxLength_ReturnsThatValue()
	{
		var registry = BuildRegistry(
			Property("comment", "string", maxLength: 32),
			Property("note", "string", maxLength: 32));

		var result = registry.GetStringMaxLength();

		result.Should().Be(32);
	}

	[Fact]
	public void GetStringMaxLength_StringPropertiesWithDifferentMaxLength_Throws()
	{
		var registry = BuildRegistry(
			Property("comment", "string", maxLength: 32),
			Property("note", "string", maxLength: 64));

		var action = registry.GetStringMaxLength;

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*comment*")
			.WithMessage("*note*");
	}

	[Fact]
	public void GetStringMaxLength_StringPropertyWithNullMaxLength_Throws()
	{
		var registry = BuildRegistry(
			Property("comment", "string", maxLength: null));

		var action = registry.GetStringMaxLength;

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*comment*");
	}

	[Fact]
	public void GetStringMaxLength_NoStringProperty_Throws()
	{
		var registry = BuildRegistry(
			Property("temperature", "float", maxLength: null));

		var action = registry.GetStringMaxLength;

		action.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void GetStringMaxLength_IgnoresNonStringProperties()
	{
		var registry = BuildRegistry(
			Property("comment", "string", maxLength: 16),
			Property("temperature", "float", maxLength: null),
			Property("count", "int", maxLength: null));

		var result = registry.GetStringMaxLength();

		result.Should().Be(16);
	}

	private static PropertyTypeDefinition Property(string id, string systemType, int? maxLength)
	{
		return new PropertyTypeDefinition(
			Id: id,
			SystemType: systemType,
			FormatKind: "numeric",
			Units: null,
			Min: null,
			Max: null,
			MaxLength: maxLength);
	}

	private static RecipeMetadataRegistry BuildRegistry(params PropertyTypeDefinition[] properties)
	{
		var propertyMap = new Dictionary<string, PropertyTypeDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var property in properties)
		{
			propertyMap[property.Id] = property;
		}

		var config = new AppConfiguration(
			Properties: propertyMap,
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default);

		return new RecipeMetadataRegistry(config);
	}
}
