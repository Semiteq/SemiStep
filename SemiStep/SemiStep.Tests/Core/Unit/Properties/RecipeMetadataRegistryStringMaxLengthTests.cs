using FluentAssertions;

using SemiStep.Tests.Helpers;

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
		var registry = TestRecipeMetadataRegistryFactory.Build(new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: 32)
		});

		var result = registry.GetStringMaxLength();

		result.Should().Be(32);
	}

	[Fact]
	public void GetStringMaxLength_MultipleStringPropertiesWithSameMaxLength_ReturnsThatValue()
	{
		var registry = TestRecipeMetadataRegistryFactory.Build(new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: 32),
			TestPropertyTypeDefinitionBuilder.CreateString("note", maxLength: 32)
		});

		var result = registry.GetStringMaxLength();

		result.Should().Be(32);
	}

	[Fact]
	public void Constructor_StringPropertiesWithDifferentMaxLength_Throws()
	{
		var action = () => TestRecipeMetadataRegistryFactory.Build(new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: 32),
			TestPropertyTypeDefinitionBuilder.CreateString("note", maxLength: 64)
		});

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*comment*")
			.WithMessage("*note*");
	}

	[Fact]
	public void Constructor_StringPropertyWithNullMaxLength_Throws()
	{
		var action = () => TestRecipeMetadataRegistryFactory.Build(new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: null)
		});

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*max_length*")
			.WithMessage("*comment*");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_StringPropertyWithNonPositiveMaxLength_Throws(int maxLength)
	{
		var action = () => TestRecipeMetadataRegistryFactory.Build(new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: maxLength)
		});

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*comment*");
	}

	[Fact]
	public void Constructor_NoStringProperty_Throws()
	{
		var action = () => TestRecipeMetadataRegistryFactory.Build(new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateFloat("temperature")
		});

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*no property*")
			.WithMessage("*system_type*");
	}

	[Fact]
	public void GetStringMaxLength_IgnoresNonStringProperties()
	{
		var registry = TestRecipeMetadataRegistryFactory.Build(new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: 16),
			TestPropertyTypeDefinitionBuilder.CreateFloat("temperature"),
			TestPropertyTypeDefinitionBuilder.CreateInt("count")
		});

		var result = registry.GetStringMaxLength();

		result.Should().Be(16);
	}
}
