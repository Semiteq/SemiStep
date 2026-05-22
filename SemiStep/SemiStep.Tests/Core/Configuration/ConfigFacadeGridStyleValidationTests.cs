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
	public async Task LoadAndValidateAsync_GridStyleMissingDisabledForegroundKey_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		// Strip only the `foreground:` line inside the `disabled:` section — `readonly:` also has its own
		// foreground key, so a global line-prefix strip would mutate two sections and weaken the assertion.
		var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
		var disabledSectionIndex = lines.FindIndex(line => line.TrimStart().StartsWith("disabled:", StringComparison.Ordinal));
		disabledSectionIndex.Should().BeGreaterThan(-1);
		var foregroundIndex = lines.FindIndex(
			disabledSectionIndex + 1,
			line => line.TrimStart().StartsWith("foreground:", StringComparison.Ordinal));
		foregroundIndex.Should().BeGreaterThan(-1);
		lines.RemoveAt(foregroundIndex);
		var mutated = string.Join(Environment.NewLine, lines);
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.disabled.foreground'"));
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMalformedDisabledHex_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		var mutated = content.Replace(
			"selected: \"#89B4D7\"",
			"selected: \"not-a-color\"");
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("colors.cells.disabled") &&
			e.Message.Contains("selected") &&
			e.Message.Contains("not-a-color"));
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMalformedDisabledDepth2PastHex_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		var mutated = content.Replace(
			"depth_2_past: \"#B5C0CC\"",
			"depth_2_past: \"not-a-color\"");
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'colors.cells.disabled.depth_2_past'") &&
			e.Message.Contains("not-a-color"));
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMalformedReadOnlyDepth2PastHex_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		var mutated = content.Replace(
			"depth_2_past: \"#ACB7C2\"",
			"depth_2_past: \"not-a-color\"");
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'colors.cells.readonly.depth_2_past'") &&
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
