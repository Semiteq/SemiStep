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
		result.Value.GridStyle.Execution.Depth0.ToString().Should().StartWith("#");
		result.Value.GridStyle.Execution.CurrentStepMarker.ToString().Should().StartWith("#");
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMissingExecutionKey_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		// Remove only the depth_0 line under `execution:` — the disabled section also has its own
		// depth_0 key, and stripping by line-prefix alone would delete both and weaken the assertion.
		var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
		var executionSectionIndex = lines.FindIndex(line => line.TrimStart().StartsWith("execution:", StringComparison.Ordinal));
		executionSectionIndex.Should().BeGreaterThan(-1);
		var depth0Index = lines.FindIndex(
			executionSectionIndex + 1,
			line => line.TrimStart().StartsWith("depth_0:", StringComparison.Ordinal));
		depth0Index.Should().BeGreaterThan(-1);
		lines.RemoveAt(depth0Index);
		var mutated = string.Join(Environment.NewLine, lines);
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.execution.depth_0'"));
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
	public async Task LoadAndValidateAsync_MissingGridStyleFile_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		File.Delete(Path.Combine(tempDir.Path, "ui", "grid_style.yaml"));

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("grid_style.yaml"));
	}
}
