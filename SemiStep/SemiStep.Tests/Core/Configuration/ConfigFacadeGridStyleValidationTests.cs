using AwesomeAssertions;

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
		result.Value.GridStyle.Execution.Running.ToString().Should().StartWith("#");
		result.Value.GridStyle.Loop.Color.ToString().Should().StartWith("#");
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMissingReadOnlyDepth0_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		// Blank only the depth_0 value under `readonly:`; the disabled section has its own depth_0 key.
		var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
		var readOnlySectionIndex = lines.FindIndex(line => line.TrimStart().StartsWith("readonly:", StringComparison.Ordinal));
		readOnlySectionIndex.Should().BeGreaterThan(-1);
		var depth0Index = lines.FindIndex(
			readOnlySectionIndex + 1,
			line => line.TrimStart().StartsWith("depth_0:", StringComparison.Ordinal));
		depth0Index.Should().BeGreaterThan(-1);
		lines[depth0Index] = "      depth_0: \"\"";
		var mutated = string.Join(Environment.NewLine, lines);
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.readonly.depth_0'"));
	}

	[Fact]
	public async Task LoadAndValidateAsync_GridStyleMalformedHex_Fails()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var gridStylePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		var token = TestContext.Current.CancellationToken;
		var content = await File.ReadAllTextAsync(gridStylePath, token);
		var mutated = content.Replace(
			"running: \"#208A3C\"",
			"running: \"not-a-color\"");
		await File.WriteAllTextAsync(gridStylePath, mutated, token);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'colors.cells.execution.running'") && e.Message.Contains("not-a-color"));
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
