using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Config")]
[Trait("Area", "AppUiOptionsLoader")]
public sealed class AppUiOptionsLoaderTests
{
	[Fact]
	public async Task LoadAndMap_PresentEnglishLocale_ResolvesEn()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(tempDir, Path.Combine("ui", "app.yaml"), "locale: en\n");

		var loadResult = await AppUiOptionsLoader.LoadAsync(tempDir.Path);

		loadResult.IsSuccess.Should().BeTrue();
		AppUiOptionsMapper.Map(loadResult.Value).Locale.Should().Be("en");
	}

	[Fact]
	public async Task LoadAndMap_PresentRussianLocale_ResolvesRu()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(tempDir, Path.Combine("ui", "app.yaml"), "locale: ru\n");

		var loadResult = await AppUiOptionsLoader.LoadAsync(tempDir.Path);

		loadResult.IsSuccess.Should().BeTrue();
		AppUiOptionsMapper.Map(loadResult.Value).Locale.Should().Be("ru");
	}

	[Fact]
	public async Task LoadAndMap_AbsentFile_DefaultsToRu()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();

		var loadResult = await AppUiOptionsLoader.LoadAsync(tempDir.Path);

		loadResult.IsSuccess.Should().BeTrue();
		loadResult.Value.Should().BeNull();
		AppUiOptionsMapper.Map(loadResult.Value).Locale.Should().Be(AppUiOptions.DefaultLocale);
	}

	[Fact]
	public async Task LoadAndMap_BlankLocale_DefaultsToRu()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(tempDir, Path.Combine("ui", "app.yaml"), "locale: \"   \"\n");

		var loadResult = await AppUiOptionsLoader.LoadAsync(tempDir.Path);

		loadResult.IsSuccess.Should().BeTrue();
		AppUiOptionsMapper.Map(loadResult.Value).Locale.Should().Be(AppUiOptions.DefaultLocale);
	}

	[Fact]
	public async Task LoadAndMap_GarbageLocale_StoredAsIs()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(tempDir, Path.Combine("ui", "app.yaml"), "locale: not-a-culture\n");

		var loadResult = await AppUiOptionsLoader.LoadAsync(tempDir.Path);

		loadResult.IsSuccess.Should().BeTrue();
		AppUiOptionsMapper.Map(loadResult.Value).Locale.Should().Be("not-a-culture");
	}
}
