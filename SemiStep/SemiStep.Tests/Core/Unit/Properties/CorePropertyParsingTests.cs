using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Properties;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "PropertyParsing")]
public sealed class CorePropertyParsingTests
{
	[Fact]
	public void NonNumericString_AsInt_ReturnsFailure()
	{
		var definition = TestPropertyTypeDefinitionBuilder.CreateInt("test_int");

		var result = PropertyParser.Parse("abc", definition);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void String_ExceedingMaxLength_ReturnsFail()
	{
		var propertyDefinition = TestPropertyTypeDefinitionBuilder.CreateString("test_string", maxLength: 10);

		var longString = new string('A', 11);

		var result = PropertyValidator.Validate(propertyDefinition, longString);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void String_ContainingEmbeddedNul_ReturnsFail()
	{
		var propertyDefinition = TestPropertyTypeDefinitionBuilder.CreateString("test_string", maxLength: 10);

		var stringWithNul = "abc\0def";

		var result = PropertyValidator.Validate(propertyDefinition, stringWithNul);

		result.IsFailed.Should().BeTrue();
	}
}
