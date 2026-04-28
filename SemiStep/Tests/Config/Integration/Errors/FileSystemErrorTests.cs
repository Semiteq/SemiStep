using FluentAssertions;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Shared;

using Tests.Config.Helpers;

using Xunit;

namespace Tests.Config.Integration.Errors;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "FileSystemErrors")]
public sealed class FileSystemErrorTests
{
	[Fact]
	public async Task MissingConfigDirectory_HasError()
	{
		var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

		var result = await ConfigFacade.LoadAndValidateAsync(nonExistentPath);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
	}

	[Theory]
	[InlineData("MissingPropertiesDir", "properties")]
	[InlineData("MissingColumnsDir", "columns")]
	[InlineData("MissingActionsDir", "actions")]
	public async Task MissingRequiredDirectory_HasError(string caseName, string directoryKeyword)
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync(caseName);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains(directoryKeyword, StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EmptyPropertiesDirectory_HasError()
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync("EmptyPropertiesDir");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("No YAML files found", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("properties", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EmptyActionsDirectory_HasError()
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync("EmptyActionsDir");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("No YAML files found", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("actions", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MalformedYaml_HasError()
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync("MalformedYaml");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Failed to parse", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("parse", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task EmptyYamlFile_HasWarning()
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync("EmptyYamlFile");

		var warnings = result.Reasons.OfType<Warning>().Select(w => w.Message).ToList();
		warnings.Should().Contain(w =>
			w.Contains("Empty", StringComparison.OrdinalIgnoreCase) ||
			w.Contains("invalid", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ConfigurationNotProducedOnError()
	{
		var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

		var result = await ConfigFacade.LoadAndValidateAsync(nonExistentPath);

		result.IsFailed.Should().BeTrue(
			"Configuration should not be produced when errors occur during loading");
	}

	[Fact]
	public async Task MultipleErrors_AllReported()
	{
		var result = await ConfigTestHelper.LoadStandaloneCaseAsync("MultipleErrors");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().HaveCountGreaterThanOrEqualTo(2,
			"both invalid system_type and min > max errors should be reported");
	}
}
