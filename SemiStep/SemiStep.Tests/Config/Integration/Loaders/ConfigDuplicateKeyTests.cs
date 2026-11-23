using AwesomeAssertions;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Configuration.Loaders;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loaders;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "DuplicateKeys")]
public sealed class ConfigDuplicateKeyTests
{
	private const string IntProperty = "  system_type: int\n  format_kind: numeric\n  units: \"\"\n";

	[Fact]
	public async Task MergeFolder_WithDifferentKeysInTwoFiles_MergesBoth()
	{
		using var tempDir = PrepareProperties(("a.yaml", $"alpha:\n{IntProperty}"), ("b.yaml", $"beta:\n{IntProperty}"));

		var result = await PropertiesSectionLoader.LoadAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue();
		result.Value.Select(p => p.PropertyTypeId).Should().Equal("alpha", "beta");
	}

	[Fact]
	public async Task MergeFolder_WithKeyInTwoFiles_ReportsKeyAndBothFiles()
	{
		using var tempDir = PrepareProperties(("a.yaml", $"alpha:\n{IntProperty}"), ("b.yaml", $"alpha:\n{IntProperty}"));

		var result = await PropertiesSectionLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.OfType<DuplicateKeyError>().Should().ContainSingle()
			.Which.Should().Match<DuplicateKeyError>(e =>
				e.Key == "alpha" && e.FirstFileName == "a.yaml" && e.RepeatFileName == "b.yaml");
	}

	[Fact]
	public async Task MergeFolder_WithCaseOnlyRepeatInOneFile_ReportsSameFileTwice()
	{
		using var tempDir = PrepareProperties(("a.yaml", $"Int:\n{IntProperty}\nint:\n{IntProperty}"));

		var result = await PropertiesSectionLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.OfType<DuplicateKeyError>().Should().ContainSingle()
			.Which.Should().Match<DuplicateKeyError>(e =>
				e.FirstFileName == "a.yaml" && e.RepeatFileName == "a.yaml");
	}

	[Fact]
	public async Task MergeFolder_ReadsFilesInOrdinalOrder()
	{
		using var tempDir = PrepareProperties(("a.yaml", $"alpha:\n{IntProperty}"), ("B.yaml", $"alpha:\n{IntProperty}"));

		var result = await PropertiesSectionLoader.LoadAsync(tempDir.Path);

		result.Errors.OfType<DuplicateKeyError>().Should().ContainSingle()
			.Which.Should().Match<DuplicateKeyError>(e =>
				e.FirstFileName == "B.yaml" && e.RepeatFileName == "a.yaml");
	}

	[Fact]
	public async Task MergeFolder_WithExactRepeatInOneFile_FailsAsParseErrorNamingKey()
	{
		using var tempDir = PrepareProperties(("a.yaml", $"alpha:\n{IntProperty}alpha:\n{IntProperty}"));

		var result = await PropertiesSectionLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.OfType<DuplicateKeyError>().Should().BeEmpty();
		result.Errors.Should().ContainSingle(e =>
			e.Message.Contains("a.yaml") && e.Message.Contains("alpha"));
	}

	[Fact]
	public async Task GridStyleFile_WithExactRepeatedKey_StartupErrorNamesKey()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var path = Path.Combine(tempDir.Path, "ui", "grid_style.yaml");
		File.AppendAllText(path, "\nlayout:\n  row_height: 30\n");

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle(e =>
			e.Message.Contains("grid_style.yaml") && e.Message.Contains("layout"));
	}

	[Fact]
	public async Task ConnectionFile_WithExactRepeatedKey_FailsAsParseErrorNamingKey()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var path = Path.Combine(tempDir.Path, "connection", "connection.yaml");
		File.AppendAllText(path, "\nplc_rack: 1\n");

		var result = await ConnectionLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle(e => e.Message.Contains("plc_rack"));
	}

	private static TempDirectory PrepareProperties(params (string FileName, string Content)[] files)
	{
		var tempDir = TestDataCopier.CreateEmptyTempDirectory();

		foreach (var (fileName, content) in files)
		{
			TestDataCopier.WriteYaml(tempDir, Path.Combine("properties", fileName), content);
		}

		return tempDir;
	}
}
