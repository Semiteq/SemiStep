using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Component", "Config")]
[Trait("Area", "StatusBar")]
[Trait("Category", "Unit")]
public sealed class StatusBarStyleConfigTests
{
	private const string DeletedStatusBarKeys = """
		status_bar:
		  item_spacing: 10
		  timer_label_font_size: 12
		  timer_label_weight: 400
		  timer_label_italic: false
		  timer_value_font_size: 20
		  timer_value_weight: 400
		  timer_value_italic: false
		""";

	private const string DeletedChromeKeys = """
		chrome:
		  local_mode: "#6C707E"
		  connecting: "#FFAF0F"
		""";

	[Fact]
	public async Task GridStyleWithDeletedKeys_LoadsWithoutError()
	{
		using var tempDir = new TempDirectory();
		var uiDir = Path.Combine(tempDir.Path, "ui");
		Directory.CreateDirectory(uiDir);
		var shipped = await File.ReadAllTextAsync(
			Path.Combine(ShippedConfigLocator.GetConfigDirectory("MBE"), "ui", "grid_style.yaml"),
			TestContext.Current.CancellationToken);
		var withDeletedKeys = shipped.Replace("\r\n", "\n")
			.Replace("status_bar:\n", DeletedStatusBarKeys.Replace("\r\n", "\n") + "\n")
			.Replace("chrome:\n", DeletedChromeKeys.Replace("\r\n", "\n") + "\n");
		withDeletedKeys.Should().Contain("timer_value_font_size").And.Contain("local_mode");
		await File.WriteAllTextAsync(
			Path.Combine(uiDir, "grid_style.yaml"), withDeletedKeys, TestContext.Current.CancellationToken);

		var loaded = await GridStyleLoader.LoadAsync(tempDir.Path);

		loaded.IsSuccess.Should().BeTrue();
		GridStyleMapper.Map(loaded.Value).IsSuccess.Should().BeTrue();
	}

	[Theory]
	[InlineData("MBE")]
	[InlineData("MOCVD")]
	[InlineData("RIE")]
	public async Task ShippedPreset_CarriesTheStatusBarPalette(string equipment)
	{
		var loaded = await GridStyleLoader.LoadAsync(ShippedConfigLocator.GetConfigDirectory(equipment));
		var options = GridStyleMapper.Map(loaded.Value).Value;

		AssertStatusBarPalette(options);
	}

	[Fact]
	public void CoreDefault_CarriesTheStatusBarPalette()
	{
		AssertStatusBarPalette(GridStyleOptions.Default);
	}

	private static void AssertStatusBarPalette(GridStyleOptions options)
	{
		options.StatusBar.Background.Should().Be(StyleColor.Parse("#F7F8FA"));
		options.StatusBar.Padding.Should().Be(5);
		options.Chrome.SecondaryForeground.Should().Be(StyleColor.Parse("#6C707E"));
		options.Chrome.Connected.Should().Be(StyleColor.Parse("#208A3C"));
		options.Chrome.Disconnected.Should().Be(StyleColor.Parse("#DB3B4B"));
	}
}
