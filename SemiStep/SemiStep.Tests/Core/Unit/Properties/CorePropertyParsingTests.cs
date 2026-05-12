using FluentAssertions;

using SemiStep.Core.Recipes;

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
		var parser = new PropertyParser();
		var definition = new PropertyTypeDefinition(
			Id: "test_int",
			SystemType: "int",
			FormatKind: "numeric",
			Units: null,
			Min: null,
			Max: null,
			MaxLength: null);

		var result = parser.Parse("abc", definition);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void String_ExceedingMaxLength_ReturnsFail()
	{
		var propertyDefinition = new PropertyTypeDefinition(
			Id: "test_string",
			SystemType: "string",
			FormatKind: "numeric",
			Units: null,
			Min: null,
			Max: null,
			MaxLength: 10);

		var longString = new string('A', 11);

		var result = PropertyValidator.Validate(propertyDefinition, longString);

		result.IsFailed.Should().BeTrue();
	}
}
