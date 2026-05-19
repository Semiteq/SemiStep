using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Properties;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "PropertyValidator")]
public sealed class PropertyValidatorStringTypeMismatchTests
{
	[Fact]
	public void Validate_StringPropertyReceivesInt_ReturnsFail()
	{
		var property = TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: 8);

		var result = PropertyValidator.Validate(property, 42);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle()
			.Which.Message.Should().Contain("Expected string")
				.And.Contain("Int32")
				.And.Contain("comment");
	}

	[Fact]
	public void Validate_StringPropertyReceivesString_ReturnsOk()
	{
		var property = TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: 8);

		var result = PropertyValidator.Validate(property, "ok");

		result.IsSuccess.Should().BeTrue();
	}
}
