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
	[InlineData("FormulaIdentifierCasingMismatch", "casing that does not match recalc_order entry 'task'")]
	public async Task FormulaInvalidCase_HasExpectedError(string caseName, string expectedSubstring)
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync(caseName);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task FormulaMapperFailure_IsNotWrappedAsCaughtException()
	{
		// ActionMapper.TryMapMany returns Result.Fail for validation failures (it does not throw).
		// ConfigFacade.MapToDomain must propagate that as Result.Fail with the structured reasons intact,
		// not wrap it in the catch-all "Failed to map configuration to domain:" error path.
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("FormulaUnknownVariable");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().NotContain(e =>
			e.Message.StartsWith("Failed to map configuration to domain", StringComparison.OrdinalIgnoreCase));
		result.Errors.Should().Contain(e =>
			e.Message.Contains("unknown_var", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task FormulaUnparseable_ErrorIdentifiesActionAndTarget()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("FormulaUnparseable");

		result.IsFailed.Should().BeTrue();
		// The error must surface enough context to locate the offending entry: action id, target key, and offending expression text.
		var parseError = result.Errors.FirstOrDefault(e =>
			e.Message.Contains("failed to parse expression", StringComparison.OrdinalIgnoreCase));
		parseError.Should().NotBeNull();
		parseError!.Message.Should().Contain("Id=110");
		parseError.Message.Should().Contain("step_duration");
	}
}
