using AwesomeAssertions;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Configuration.Loaders;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "AppUiOptionsLoader")]
public sealed class AppUiOptionsLoaderTests
{
	private const string AppFile = "ui/app.yaml";

	[Theory]
	[InlineData("locale: en\n", "en")]
	[InlineData("locale: ru\n", "ru")]
	[InlineData("locale: RU\n", "ru")]
	[InlineData("locale: \" En \"\n", "en")]
	public async Task Load_SupportedLocale_ReportsTrimmedLowerCaseLocale(string content, string expected)
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, AppFile, content);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue();
		result.Value.Ui.Locale.Should().Be(expected);
	}

	[Fact]
	public async Task Load_AppFileMissing_ReportsFileName()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		File.Delete(Path.Combine(tempDir.Path, AppFile));

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.Errors.OfType<AppFileNotFoundError>().Should().ContainSingle()
			.Which.FilePath.Should().EndWith("app.yaml");
	}

	[Theory]
	[InlineData("")]
	[InlineData("locale:\n")]
	[InlineData("locale: null\n")]
	[InlineData("locale: \"   \"\n")]
	[InlineData("locle: en\n")]
	public async Task Load_LocaleMissingOrBlank_ReportsKey(string content)
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, AppFile, content);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.Errors.OfType<MissingKeyError>().Should().ContainSingle()
			.Which.Should().Match<MissingKeyError>(e => e.Key == "locale" && e.FileName == "app.yaml");
	}

	[Theory]
	[InlineData("locale: de\n", "de")]
	[InlineData("locale: eng\n", "eng")]
	public async Task Load_UnsupportedLocale_ReportsValueAndAcceptedValues(string content, string value)
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, AppFile, content);

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.Errors.OfType<UnsupportedLocaleError>().Should().ContainSingle()
			.Which.Should().Match<UnsupportedLocaleError>(e =>
				e.Value == value && e.FileName == "app.yaml" && e.AcceptedValues.SequenceEqual(new[] { "ru", "en" }));
	}

	[Fact]
	public async Task Load_AppFileWithRepeatedKey_ReportsParseErrorNamingFile()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, AppFile, "locale: ru\nlocale: en\n");

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle(e => e.Message.Contains("app.yaml"));
	}

	[Fact]
	public async Task Load_AppFileProblemWithAnotherLoadingProblem_ReportsBoth()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, AppFile, "locale: de\n");
		TestDataCopier.WriteYaml(tempDir, "ui/extra.yaml", "a: 1\n");

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.Errors.OfType<UnsupportedLocaleError>().Should().ContainSingle();
		result.Errors.OfType<UnexpectedFileError>().Should().ContainSingle();
	}
}
