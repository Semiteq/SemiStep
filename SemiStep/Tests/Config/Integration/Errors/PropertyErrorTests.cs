using FluentAssertions;

using Tests.Config.Helpers;

using Xunit;

namespace Tests.Config.Integration.Errors;

/// <summary>
/// Tests for property validation errors during config loading.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Feature", "PropertyValidation")]
public class PropertyErrorTests
{
	[Fact]
	public async Task DuplicatePropertyId_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("DuplicatePropertyId");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("Duplicate PropertyTypeId", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task DuplicatePropertyId_IdentifiesDuplicateId()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("DuplicatePropertyId");

		// Assert
		context.Errors.Should().Contain(e =>
			e.Message.Contains("int", StringComparison.OrdinalIgnoreCase),
			"error should identify 'int' as the duplicate PropertyTypeId");
	}

	[Fact]
	public async Task InvalidSystemType_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("InvalidSystemType");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("SystemType must be one of", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task InvalidSystemType_ShowsInvalidValue()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("InvalidSystemType");

		// Assert
		context.Errors.Should().Contain(e =>
			e.Message.Contains("boolean", StringComparison.OrdinalIgnoreCase),
			"error should show 'boolean' as the invalid value");
	}

	[Fact]
	public async Task MinGreaterThanMax_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MinGreaterThanMax");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("Min", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("Max", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MinGreaterThanMax_ShowsValues()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MinGreaterThanMax");

		// Assert
		context.Errors.Should().Contain(e =>
			e.Message.Contains("100") && e.Message.Contains("10"),
			"error should show the actual Min (100) and Max (10) values");
	}

	[Fact]
	public async Task PropertyWithMissingSystemType_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingSystemType");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("SystemType is required", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task PropertyWithMissingFormatKind_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingFormatKind");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("FormatKind is required", StringComparison.OrdinalIgnoreCase));
	}
}
