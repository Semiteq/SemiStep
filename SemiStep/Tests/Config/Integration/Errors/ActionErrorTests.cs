using FluentAssertions;

using Tests.Config.Helpers;

using Xunit;

namespace Tests.Config.Integration.Errors;

/// <summary>
/// Tests for action validation errors during config loading.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Feature", "ActionValidation")]
public class ActionErrorTests
{
	[Fact]
	public async Task DuplicateActionId_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("DuplicateActionId");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("Duplicate action Id", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task DuplicateActionId_IdentifiesDuplicateId()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("DuplicateActionId");

		// Assert
		context.Errors.Should().Contain(e =>
				e.Message.Contains("10"),
			"error should identify '10' as the duplicate action Id");
	}

	[Fact]
	public async Task InvalidDeployDuration_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("InvalidDeployDuration");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("DeployDuration must be", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("immediate", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task InvalidDeployDuration_ShowsInvalidValue()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("InvalidDeployDuration");

		// Assert
		context.Errors.Should().Contain(e =>
				e.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase),
			"error should show 'invalid' as the invalid value");
	}

	[Fact]
	public async Task ActionWithMissingUiName_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingUiName");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("UiName is required", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ActionWithMissingDeployDuration_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingDeployDuration");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("DeployDuration is required", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ActionWithMissingColumnKey_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingColumnKey");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("column Key is required", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ActionWithMissingColumnPropertyTypeId_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingColumnPropertyTypeId");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("PropertyTypeId is required", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ActionWithZeroId_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("ActionWithZeroId");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("Id must be positive", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ActionWithNegativeId_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("ActionWithNegativeId");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("Id must be positive", StringComparison.OrdinalIgnoreCase));
	}
}
