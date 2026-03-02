using Config.Facade;

using FluentAssertions;

using Tests.Config.Helpers;

using Xunit;

namespace Tests.Config.Integration.Errors;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Feature", "FileSystemErrors")]
public class FileSystemErrorTests
{
	[Fact]
	public async Task MissingConfigDirectory_HasError()
	{
		var facade = ConfigTestHelper.CreateFacade();
		var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

		var context = await ConfigFacade.LoadAsync(nonExistentPath);

		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingPropertiesDirectory_HasError()
	{
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingPropertiesDir");

		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("properties", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingColumnsDirectory_HasError()
	{
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingColumnsDir");

		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("columns", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingActionsDirectory_HasError()
	{
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MissingActionsDir");

		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("actions", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EmptyPropertiesDirectory_HasError()
	{
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("EmptyPropertiesDir");

		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("No YAML files found", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("properties", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EmptyActionsDirectory_HasError()
	{
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("EmptyActionsDir");

		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("No YAML files found", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("actions", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MalformedYaml_HasError()
	{
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MalformedYaml");

		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("Failed to parse", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("parse", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EmptyYamlFile_HasWarning()
	{
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("EmptyYamlFile");

		context.HasWarnings.Should().BeTrue();
		context.Warnings.Should().Contain(w =>
			w.Message.Contains("Empty", StringComparison.OrdinalIgnoreCase) ||
			w.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ConfigurationIsNullOnError()
	{
		var facade = ConfigTestHelper.CreateFacade();
		var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

		var context = await ConfigFacade.LoadAsync(nonExistentPath);

		context.HasErrors.Should().BeTrue();
		context.Configuration.Should().BeNull(
			"Configuration should be null when errors occur during loading");
	}

	[Fact]
	public async Task MultipleErrors_AllReported()
	{
		var context = await ConfigTestHelper.LoadStandaloneCaseAsync("MultipleErrors");

		context.HasErrors.Should().BeTrue();
		context.Errors.Should().HaveCountGreaterThanOrEqualTo(2,
			"both invalid system_type and min > max errors should be reported");
	}
}
