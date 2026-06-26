using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Component", "Config")]
[Trait("Category", "Unit")]
[Trait("Area", "GridStyleEditorFacade")]
public sealed class GridStyleEditorFacadeTests
{
	[Theory]
	[InlineData("MBE")]
	[InlineData("MOCVD")]
	[InlineData("RIE")]
	public async Task Load_ShippedConfig_ReturnsPopulatedRecord(string equipment)
	{
		using var tempDir = CopyShippedConfig(equipment);

		var result = await new GridStyleEditorFacade().Load(tempDir.Path);

		result.IsSuccess.Should().BeTrue();
		result.Value.SelectionBackgroundColor.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Load_MissingConfigDir_ReturnsFail()
	{
		var missingDir = Path.Combine(Path.GetTempPath(), "SemiStep.NoSuchConfig", Guid.NewGuid().ToString("N"));

		var result = await new GridStyleEditorFacade().Load(missingDir);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public async Task SaveThenLoad_RoundTrips()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var facade = new GridStyleEditorFacade();

		var original = (await facade.Load(tempDir.Path)).Value;

		facade.Save(tempDir.Path, original).IsSuccess.Should().BeTrue();

		var reloaded = (await facade.Load(tempDir.Path)).Value;

		reloaded.Should().Be(original);
	}

	[Fact]
	public async Task Validate_ShippedRecord_ReturnsOk()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var facade = new GridStyleEditorFacade();

		var loaded = (await facade.Load(tempDir.Path)).Value;

		facade.Validate(loaded).IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_MalformedColor_ReturnsFail()
	{
		var invalid = GridStyleOptions.Default with { ExecutionDepth0Color = "not-a-color" };

		new GridStyleEditorFacade().Validate(invalid).IsFailed.Should().BeTrue();
	}

	[Fact]
	public async Task Save_InvalidRecord_FailsValidationGuardBeforeWriting()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var facade = new GridStyleEditorFacade();
		var filePath = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");

		var token = TestContext.Current.CancellationToken;
		var before = await File.ReadAllTextAsync(filePath, token);

		var invalid = (await facade.Load(tempDir.Path)).Value
			with
		{ ExecutionDepth0Color = "not-a-color" };

		facade.Save(tempDir.Path, invalid).IsFailed.Should().BeTrue();

		var after = await File.ReadAllTextAsync(filePath, token);
		after.Should().Be(before);
	}

	private static TempDirectory CopyShippedConfig(string equipment)
	{
		var source = ShippedConfigLocator.GetConfigDirectory(equipment);
		var tempDir = new TempDirectory();
		var uiDir = Path.Combine(tempDir.Path, "ui");
		Directory.CreateDirectory(uiDir);
		File.Copy(
			Path.Combine(source, "ui", "grid_style.yaml"),
			Path.Combine(uiDir, "grid_style.yaml"),
			overwrite: true);
		return tempDir;
	}
}
