using FluentAssertions;

using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Errors;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "ActionValidation")]
public sealed class ActionErrorTests
{
	[Fact]
	public async Task DuplicateActionId_HasError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("DuplicateActionId");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Duplicate action Id", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task DuplicateActionId_IdentifiesDuplicateId()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("DuplicateActionId");

		result.Errors.Should().Contain(e =>
				e.Message.Contains("10"),
			"error should identify '10' as the duplicate action Id");
	}

	[Fact]
	public async Task InvalidDeployDuration_HasError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("InvalidDeployDuration");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("DeployDuration must be", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("immediate", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task InvalidDeployDuration_ShowsInvalidValue()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("InvalidDeployDuration");

		result.Errors.Should().Contain(e =>
				e.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase),
			"error should show 'invalid' as the invalid value");
	}

	[Theory]
	[InlineData("MissingUiName", "UiName is required")]
	[InlineData("MissingDeployDuration", "DeployDuration is required")]
	[InlineData("MissingColumnKey", "column Key is required")]
	[InlineData("MissingColumnPropertyTypeId", "PropertyTypeId is required")]
	[InlineData("ActionWithZeroId", "Id must be positive")]
	[InlineData("ActionWithNegativeId", "Id must be positive")]
	public async Task StandaloneCase_HasExpectedError(string caseName, string expectedSubstring)
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync(caseName);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase));
	}

	[Theory]
	[InlineData("FormulaUnknownVariable", "unknown_var")]
	[InlineData("FormulaUnparseable", "failed to parse")]
	[InlineData("FormulaMissingExpression", "missing entry for recalc_order variable 'initial_value'")]
	[InlineData("FormulaRecalcOrderUnknownColumn", "'pressure' is not a column of this action")]
	public async Task FormulaInvalidCase_HasExpectedError(string caseName, string expectedSubstring)
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync(caseName);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase));
	}
}
