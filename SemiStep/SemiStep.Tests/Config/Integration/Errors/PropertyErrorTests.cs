using FluentAssertions;

using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Errors;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "PropertyValidation")]
public sealed class PropertyErrorTests
{
	[Fact]
	public async Task DuplicatePropertyId_HasError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("DuplicatePropertyId");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Duplicate PropertyTypeId", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task DuplicatePropertyId_IdentifiesDuplicateId()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("DuplicatePropertyId");

		result.Errors.Should().Contain(e =>
				e.Message.Contains("int", StringComparison.OrdinalIgnoreCase),
			"error should identify 'int' as the duplicate PropertyTypeId");
	}

	[Fact]
	public async Task InvalidSystemType_HasError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("InvalidSystemType");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("SystemType must be one of", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task InvalidSystemType_ShowsInvalidValue()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("InvalidSystemType");

		result.Errors.Should().Contain(e =>
				e.Message.Contains("boolean", StringComparison.OrdinalIgnoreCase),
			"error should show 'boolean' as the invalid value");
	}

	[Fact]
	public async Task MinGreaterThanMax_HasError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("MinGreaterThanMax");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Min", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("Max", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MinGreaterThanMax_ShowsValues()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("MinGreaterThanMax");

		result.Errors.Should().Contain(e =>
				e.Message.Contains("100") && e.Message.Contains("10"),
			"error should show the actual Min (100) and Max (10) values");
	}

	[Fact]
	public async Task PropertyWithMissingSystemType_HasError()
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingSystemType");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("SystemType is required", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task PropertyWithMissingFormatKind_HasError()
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingFormatKind");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("FormatKind is required", StringComparison.OrdinalIgnoreCase));
	}
}
