using FluentAssertions;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "GridStyleValidation")]
public sealed class ConfigFacadeGridStyleValidationTests
{
	[Fact]
	public async Task LoadAndValidateAsync_StandardFixture_LoadsSuccessfully()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue();
		result.Value.GridStyle.ExecutionDepth0Color.Should().StartWith("#");
		result.Value.GridStyle.ExecutionCurrentStepMarkerColor.Should().StartWith("#");
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMissingExecutionKey_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		// Remove the depth_0 key line entirely to simulate a missing field.
		var mutated = string.Join(Environment.NewLine,
			content.Split(["\r\n", "\n"], StringSplitOptions.None)
				.Where(line => !line.TrimStart().StartsWith("depth_0:", StringComparison.Ordinal)));
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("depth_0"));
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMalformedHex_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		var mutated = content.Replace(
			"current_step_marker: \"#FF8800\"",
			"current_step_marker: \"not-a-color\"");
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("current_step_marker") && e.Message.Contains("not-a-color"));
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMissingDisabledForegroundKey_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		var mutated = string.Join(Environment.NewLine,
			content.Split(["\r\n", "\n"], StringSplitOptions.None)
				.Where(line => !line.TrimStart().StartsWith("foreground:", StringComparison.Ordinal)));
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("colors.cells.disabled") && e.Message.Contains("foreground"));
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMalformedDisabledHex_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		var mutated = content.Replace(
			"normal: \"#E0E0E0\"",
			"normal: \"not-a-color\"");
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("colors.cells.disabled") &&
			e.Message.Contains("normal") &&
			e.Message.Contains("not-a-color"));
	}

	[Fact]
	public async Task LoadAndValidateAsync_MissingGridStyleFile_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		File.Delete(Path.Combine(tempDir.Path, "ui", "grid_style.yaml"));

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("grid_style.yaml"));
	}
}
