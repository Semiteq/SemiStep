using FluentAssertions;

using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Shared;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loaders;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "FailLoud")]
public sealed class LoaderFailLoudTests
{
	[Fact]
	public async Task ConnectionLoader_FailsOnMissingDirectory()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();

		var result = await ConnectionLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Required config not found", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("connection", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ConnectionLoader_FailsOnMissingFile()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		Directory.CreateDirectory(Path.Combine(tempDir.Path, "connection"));

		var result = await ConnectionLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Required config not found", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("connection.yaml", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ConnectionLoader_FailsOnUnparseableYaml()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(
			tempDir,
			Path.Combine("connection", "connection.yaml"),
			": : : not valid yaml @@@ {[");

		var result = await ConnectionLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Failed to load", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("connection.yaml", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task GridStyleLoader_FailsOnUnparseableYaml()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(
			tempDir,
			Path.Combine("ui", "grid_style.yaml"),
			": : : not valid yaml @@@ {[");

		var result = await GridStyleLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Failed to load", StringComparison.OrdinalIgnoreCase) &&
			e.Message.Contains("grid_style.yaml", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task GridStyleLoader_SucceedsWithWarning_OnMissingDirectory()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();

		var result = await GridStyleLoader.LoadAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue("grid styles are cosmetic — missing directory is allowed");
		result.Reasons.OfType<Warning>().Should().Contain(w =>
			w.Message.Contains("UI directory not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task GridStyleLoader_SucceedsWithWarning_OnMissingFile()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		Directory.CreateDirectory(Path.Combine(tempDir.Path, "ui"));

		var result = await GridStyleLoader.LoadAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue("grid styles are cosmetic — missing file is allowed");
		result.Reasons.OfType<Warning>().Should().Contain(w =>
			w.Message.Contains("Grid style file not found", StringComparison.OrdinalIgnoreCase));
	}
}
