using FluentAssertions;

using SemiStep.Core.Configuration.Loaders;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "GridStyleLoader")]
public sealed class GridStyleLoaderMissingFileTests
{
	[Fact]
	public async Task LoadAsync_MissingUiDirectory_Fails()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();

		var result = await GridStyleLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Grid style config not found", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("ui", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task LoadAsync_MissingGridStyleFile_Fails()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		Directory.CreateDirectory(Path.Combine(tempDir.Path, "ui"));

		var result = await GridStyleLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Grid style config not found", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("grid_style.yaml", StringComparison.OrdinalIgnoreCase));
	}
}
