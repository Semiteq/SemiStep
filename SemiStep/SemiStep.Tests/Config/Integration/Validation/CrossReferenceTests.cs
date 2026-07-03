using FluentAssertions;

using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Validation;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "CrossReferenceValidation")]
public sealed class CrossReferenceTests
{
	[Theory]
	[InlineData("MissingPropertyReference", "unknown property_type_id", "nonexistent_property")]
	[InlineData("MissingColumnReference", "unknown column", "nonexistent_column")]
	[InlineData("MissingGroupReference", "unknown group_name", "nonexistent_group")]
	[InlineData("MissingActionPropertyReference", "unknown property_type_id", "nonexistent_property")]
	public async Task InvalidReference_HasErrorIdentifyingUnknownId(
		string caseName, string expectedErrorSubstring, string expectedUnknownId)
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync(caseName);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains(expectedErrorSubstring, StringComparison.OrdinalIgnoreCase));
		result.Errors.Should().Contain(e =>
			e.Message.Contains(expectedUnknownId, StringComparison.OrdinalIgnoreCase),
			$"error should identify '{expectedUnknownId}' as the unknown reference");
	}

	[Fact]
	public async Task ErrorLocation_IncludesColumnKey()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("MissingPropertyReference");

		result.Errors.Should().Contain(e =>
				e.Message.Contains("step_duration"),
			"error should reference the column 'step_duration' that has the broken reference");
	}

	[Fact]
	public async Task ErrorLocation_IncludesActionInfo()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("MissingColumnReference");

		result.Errors.Should().Contain(e =>
				e.Message.Contains("Wait") || e.Message.Contains("10"),
			"error should reference the action 'Wait' (Id=10) that has the broken reference");
	}
}
