using AwesomeAssertions;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Configuration.Loaders;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loaders;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "FolderPolicy")]
public sealed class ConfigFolderPolicyTests
{
	[Fact]
	public async Task ConnectionFolder_WithExtraYaml_ReportsUnexpectedFile()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, "connection/connection_test.yaml", "a: 1\n");

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.OfType<UnexpectedFileError>().Should().ContainSingle()
			.Which.Should().Match<UnexpectedFileError>(e =>
				e.FileName == "connection_test.yaml"
				&& e.FolderName == "connection"
				&& e.AcceptedNames.SequenceEqual(new[] { "connection.yaml" }));
	}

	[Fact]
	public async Task UiFolder_WithExtraYaml_ReportsExactlyOneError()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, "ui/extra.yaml", "a: 1\n");

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.Errors.Should().ContainSingle();
		result.Errors.Single().Should().BeOfType<UnexpectedFileError>()
			.Which.Should().Match<UnexpectedFileError>(e =>
				e.FileName == "extra.yaml"
				&& e.AcceptedNames.SequenceEqual(new[] { "grid_style.yaml", "app.yaml" }));
	}

	[Theory]
	[InlineData("connection", "connection.yml")]
	[InlineData("ui", "extra.yml")]
	public async Task NamedFolder_WithYmlFile_ReportsUnexpectedFile(string folder, string fileName)
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, $"{folder}/{fileName}", "a: 1\n");

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.Errors.OfType<UnexpectedFileError>().Should().ContainSingle(e => e.FileName == fileName);
	}

	[Fact]
	public async Task ConnectionFolder_WithOnlyYml_ReportsUnexpectedFileAndMissingRequiredConfig()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var connectionDir = Path.Combine(tempDir.Path, "connection");
		File.Move(Path.Combine(connectionDir, "connection.yaml"), Path.Combine(connectionDir, "connection.yml"));

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.Errors.OfType<UnexpectedFileError>().Should().ContainSingle();
		result.Errors.Should().Contain(e => e.Message.Contains("Required config not found"));
	}

	[Fact]
	public async Task MergeFolder_WithOnlyYml_ReportsUnexpectedFileAndMissingSection()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var groupsDir = Path.Combine(tempDir.Path, "groups");
		File.Move(Path.Combine(groupsDir, "placeholder.yaml"), Path.Combine(groupsDir, "a.yml"));

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.Errors.OfType<UnexpectedFileError>().Should().ContainSingle(e =>
			e.FileName == "a.yml" && e.AcceptedNames.SequenceEqual(new[] { "*.yaml" }));
		result.Errors.Should().Contain(e => e.Message.Contains("No YAML files found"));
	}

	[Fact]
	public async Task ConnectionFile_WithUppercaseName_IsAcceptedAndRead()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		var connectionDir = Path.Combine(tempDir.Path, "connection");
		var content = File.ReadAllText(Path.Combine(connectionDir, "connection.yaml"));
		File.Delete(Path.Combine(connectionDir, "connection.yaml"));
		File.WriteAllText(Path.Combine(connectionDir, "CONNECTION.YAML"), content);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue();
	}

	[Theory]
	[InlineData("connection/connection.tmp")]
	[InlineData("ui/.grid_style.abc.tmp")]
	[InlineData("ui/app.bak")]
	[InlineData("groups/notes.yaml2")]
	[InlineData("properties/readme.md")]
	public async Task NonYamlFile_InSectionFolder_IsIgnored(string relativePath)
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, relativePath, "not: [valid");

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public async Task MissingGroupsFolder_ReportsOnlyTheLoaderError()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		Directory.Delete(Path.Combine(tempDir.Path, "groups"), recursive: true);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.Errors.Should().ContainSingle().Which.Message.Should().Contain("Groups directory not found");
	}
}
