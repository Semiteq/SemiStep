using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.Styles;

[Trait("Component", "UI")]
[Trait("Category", "Unit")]
public sealed class CellPaletteInstallerTests
{
	[AvaloniaFact]
	public void Install_PopulatesAllCellBrushes_WithExpectedColors()
	{
		var defaults = GridStyleOptions.Default;
		var gridStyle = defaults with
		{
			Fonts = defaults.Fonts with
			{
				FontFamily = "Arial",
				CellFontSize = 12,
			},
			StatusBar = new StatusBarStyle(
				Background: StyleColor.Parse("#F0F0F0"),
				Foreground: StyleColor.Parse("#111111"),
				Padding: 5,
				FontSize: 14,
				Weight: 700,
				Italic: true),
			ValidationPanel = new ValidationPanelStyle(
				Background: StyleColor.Parse("#FBFBFB"),
				Foreground: StyleColor.Parse("#222222"),
				ErrorColor: StyleColor.Parse("#D32F2F"),
				WarningColor: StyleColor.Parse("#F57C00"),
				MaxHeight: 100),
			Chrome = new ChromeColors(
				Info: StyleColor.Parse("#1976D2"),
				Connected: StyleColor.Parse("#44BB44"),
				Disconnected: StyleColor.Parse("#FF4444"),
				PanelBackground: StyleColor.Parse("#F8F8F8"),
				PanelHeaderBackground: StyleColor.Parse("#EEEEEE"),
				SubtleBorder: StyleColor.Parse("#D0D0D0"),
				Separator: StyleColor.Parse("#C0C0C0"),
				SecondaryForeground: StyleColor.Parse("#888888"),
				GridBorder: StyleColor.Parse("#808080"),
				GridBackground: StyleColor.Parse("#FFFFFF"),
				HeaderForeground: StyleColor.Parse("#000000"),
				GridLine: StyleColor.Parse("#CCCCCC")),
		};
		var resources = new ResourceDictionary();

		CellPaletteInstaller.Install(resources, gridStyle);

		resources[CellPalette.ResourceKey].Should().BeOfType<CellPalette>();
		AssertBrush(resources, CellPaletteInstaller.GridLineBrushKey, "#CCCCCC");
		AssertBrush(resources, CellPaletteInstaller.StatusBarBackgroundBrushKey, "#F0F0F0");
		AssertBrush(resources, CellPaletteInstaller.StatusBarForegroundBrushKey, "#111111");
		AssertBrush(resources, CellPaletteInstaller.ErrorBrushKey, "#D32F2F");
		AssertBrush(resources, CellPaletteInstaller.WarningBrushKey, "#F57C00");
		AssertBrush(resources, CellPaletteInstaller.ValidationPanelBackgroundBrushKey, "#FBFBFB");
		AssertBrush(resources, CellPaletteInstaller.ValidationPanelForegroundBrushKey, "#222222");
		AssertBrush(resources, CellPaletteInstaller.InfoBrushKey, "#1976D2");
		AssertBrush(resources, CellPaletteInstaller.ConnectedBrushKey, "#44BB44");
		AssertBrush(resources, CellPaletteInstaller.DisconnectedBrushKey, "#FF4444");
		AssertBrush(resources, CellPaletteInstaller.PanelBackgroundBrushKey, "#F8F8F8");
		AssertBrush(resources, CellPaletteInstaller.PanelHeaderBackgroundBrushKey, "#EEEEEE");
		AssertBrush(resources, CellPaletteInstaller.SubtleBorderBrushKey, "#D0D0D0");
		AssertBrush(resources, CellPaletteInstaller.SeparatorBrushKey, "#C0C0C0");
		AssertBrush(resources, CellPaletteInstaller.SecondaryForegroundBrushKey, "#888888");
		AssertBrush(resources, CellPaletteInstaller.GridBorderBrushKey, "#808080");
		AssertBrush(resources, CellPaletteInstaller.GridBackgroundBrushKey, "#FFFFFF");
		AssertBrush(resources, CellPaletteInstaller.HeaderForegroundBrushKey, "#000000");

		resources[CellPaletteInstaller.StatusBarPaddingKey].Should().Be(new Thickness(0, 5, 0, 5));
		resources[CellPaletteInstaller.StatusBarFontSizeKey].Should().Be(14d);
		resources[CellPaletteInstaller.ValidationPanelMaxHeightKey].Should().Be(100d);
		resources[CellPaletteInstaller.RowHeightKey].Should().Be(gridStyle.Layout.RowHeight);

		resources[CellPaletteInstaller.AppFontFamilyKey].Should().BeOfType<FontFamily>()
			.Which.Name.Should().Be("Arial");
		resources[CellPaletteInstaller.StatusBarFontWeightKey].Should().Be(FontWeight.Bold);
		resources[CellPaletteInstaller.StatusBarFontStyleKey].Should().Be(FontStyle.Italic);

		resources.ContainsKey("StatusBarTimerFontSize").Should().BeFalse();

		resources.Count.Should().Be(29);
	}

	[AvaloniaFact]
	public void Install_EmptyFontFamily_InstallsThemeDefaultFamily()
	{
		var defaults = GridStyleOptions.Default;
		var gridStyle = defaults with { Fonts = defaults.Fonts with { FontFamily = "" } };
		var resources = new ResourceDictionary();

		CellPaletteInstaller.Install(resources, gridStyle);

		resources[CellPaletteInstaller.AppFontFamilyKey].Should().Be(FontFamily.Default);
	}

	private static void AssertBrush(IResourceDictionary resources, string key, string expectedHex)
	{
		resources.ContainsKey(key).Should().BeTrue($"resource key '{key}' must be present");
		var brush = resources[key].Should().BeOfType<SolidColorBrush>().Subject;
		brush.Color.Should().Be(Color.Parse(expectedHex));
	}
}
