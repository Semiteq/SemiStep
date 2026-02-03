using FluentAssertions;

using Tests.Config.Helpers;

using Xunit;

namespace Tests.Config.Integration.Errors;

/// <summary>
/// Tests for file system related errors during config loading.
/// Uses standalone test cases from YamlConfigs/Standalone/ directory.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Feature", "FileSystemErrors")]
public class FileSystemErrorTests
{
	[Fact]
	public async Task MissingConfigDirectory_HasError()
	{
		// Arrange
		var facade = ConfigTestHelper.CreateFacade();
		var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

		// Act
		var context = await facade.LoadAsync(nonExistentPath);

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingPropertiesDirectory_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingPropertiesDir");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("properties", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingColumnsDirectory_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingColumnsDir");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("columns", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingActionsDirectory_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingActionsDir");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("actions", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EmptyPropertiesDirectory_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("EmptyPropertiesDir");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("No YAML files found", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("properties", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EmptyActionsDirectory_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("EmptyActionsDir");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("No YAML files found", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("actions", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MalformedYaml_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MalformedYaml");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("Failed to parse", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("parse", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EmptyYamlFile_HasWarning()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("EmptyYamlFile");

		// Assert
		context.HasWarnings.Should().BeTrue();
		context.Warnings.Should().Contain(w =>
			w.Message.Contains("Empty", StringComparison.OrdinalIgnoreCase) ||
			w.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ConfigurationIsNullOnError()
	{
		// Arrange
		var facade = ConfigTestHelper.CreateFacade();
		var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

		// Act
		var context = await facade.LoadAsync(nonExistentPath);

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Configuration.Should().BeNull(
			"Configuration should be null when errors occur during loading");
	}

	[Fact]
	public async Task MultipleErrors_AllReported()
	{
		// Act
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MultipleErrors");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().HaveCountGreaterThanOrEqualTo(2,
			"both invalid system_type and min > max errors should be reported");
	}
}
