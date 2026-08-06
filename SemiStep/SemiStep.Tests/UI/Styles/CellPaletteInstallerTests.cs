using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using FluentAssertions;

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
			Selection = new SelectionColors(
				Background: StyleColor.Parse("#CCE4F7"),
				Foreground: StyleColor.Parse("#202020")),
			ChangedCells = new ChangedCellColors(
				Changed: StyleColor.Parse("#FFCC80"),
				ChangedSelected: StyleColor.Parse("#EFD3A4")),
			ReadOnlyCells = new DepthPalette(
				Depth0: StyleColor.Parse("#D8D8D8"),
				Depth1: StyleColor.Parse("#CCD5E0"),
				Depth2: StyleColor.Parse("#B8C3D1"),
				Depth3: StyleColor.Parse("#94A2B3"),
				Depth0Past: StyleColor.Parse("#C8C8C8"),
				Depth1Past: StyleColor.Parse("#BCC4CE"),
				Depth2Past: StyleColor.Parse("#ACB7C2"),
				Depth3Past: StyleColor.Parse("#8590A0"),
				Selected: StyleColor.Parse("#6B95C0"),
				Foreground: StyleColor.Parse("#606060")),
			DisabledCells = new DepthPalette(
				Depth0: StyleColor.Parse("#E0E0E0"),
				Depth1: StyleColor.Parse("#D5DEEA"),
				Depth2: StyleColor.Parse("#C2CEDB"),
				Depth3: StyleColor.Parse("#9DABBC"),
				Depth0Past: StyleColor.Parse("#D0D0D0"),
				Depth1Past: StyleColor.Parse("#C5CDD8"),
				Depth2Past: StyleColor.Parse("#B5C0CC"),
				Depth3Past: StyleColor.Parse("#909AAA"),
				Selected: StyleColor.Parse("#89B4D7"),
				Foreground: StyleColor.Parse("#808080")),
			StatusBar = new StatusBarStyle(
				Background: StyleColor.Parse("#F0F0F0"),
				Foreground: StyleColor.Parse("#111111"),
				Padding: 5,
				ItemSpacing: 10,
				FontSize: 14,
				Weight: 700,
				Italic: true,
				TimerLabelFontSize: 16,
				TimerLabelWeight: 500,
				TimerLabelItalic: true,
				TimerValueFontSize: 28,
				TimerValueWeight: 600,
				TimerValueItalic: false),
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
				LocalMode: StyleColor.Parse("#6C707E"),
				Connecting: StyleColor.Parse("#FFAF0F"),
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

		AssertBrush(resources, CellPaletteInstaller.CellReadOnlyDepth0BrushKey, "#D8D8D8");
		AssertBrush(resources, CellPaletteInstaller.CellReadOnlyDepth1BrushKey, "#CCD5E0");
		AssertBrush(resources, CellPaletteInstaller.CellReadOnlyDepth2BrushKey, "#B8C3D1");
		AssertBrush(resources, CellPaletteInstaller.CellReadOnlyDepth3BrushKey, "#94A2B3");
		AssertBrush(resources, CellPaletteInstaller.CellReadOnlyDepth0PastBrushKey, "#C8C8C8");
		AssertBrush(resources, CellPaletteInstaller.CellReadOnlyDepth1PastBrushKey, "#BCC4CE");
		AssertBrush(resources, CellPaletteInstaller.CellReadOnlyDepth2PastBrushKey, "#ACB7C2");
		AssertBrush(resources, CellPaletteInstaller.CellReadOnlyDepth3PastBrushKey, "#8590A0");
		AssertBrush(resources, CellPaletteInstaller.CellReadOnlySelectedBackgroundBrushKey, "#6B95C0");
		AssertBrush(resources, CellPaletteInstaller.CellReadOnlyForegroundBrushKey, "#606060");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledDepth0BrushKey, "#E0E0E0");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledDepth1BrushKey, "#D5DEEA");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledDepth2BrushKey, "#C2CEDB");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledDepth3BrushKey, "#9DABBC");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledDepth0PastBrushKey, "#D0D0D0");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledDepth1PastBrushKey, "#C5CDD8");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledDepth2PastBrushKey, "#B5C0CC");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledDepth3PastBrushKey, "#909AAA");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledSelectedBackgroundBrushKey, "#89B4D7");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledForegroundBrushKey, "#808080");
		AssertBrush(resources, CellPaletteInstaller.SelectionBackgroundBrushKey, "#CCE4F7");
		AssertBrush(resources, CellPaletteInstaller.SelectionForegroundBrushKey, "#202020");
		AssertBrush(resources, CellPaletteInstaller.CellChangedBrushKey, "#FFCC80");
		AssertBrush(resources, CellPaletteInstaller.CellChangedSelectedBackgroundBrushKey, "#EFD3A4");
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
		AssertBrush(resources, CellPaletteInstaller.LocalModeBrushKey, "#6C707E");
		AssertBrush(resources, CellPaletteInstaller.ConnectingBrushKey, "#FFAF0F");
		AssertBrush(resources, CellPaletteInstaller.PanelBackgroundBrushKey, "#F8F8F8");
		AssertBrush(resources, CellPaletteInstaller.PanelHeaderBackgroundBrushKey, "#EEEEEE");
		AssertBrush(resources, CellPaletteInstaller.SubtleBorderBrushKey, "#D0D0D0");
		AssertBrush(resources, CellPaletteInstaller.SeparatorBrushKey, "#C0C0C0");
		AssertBrush(resources, CellPaletteInstaller.SecondaryForegroundBrushKey, "#888888");
		AssertBrush(resources, CellPaletteInstaller.GridBorderBrushKey, "#808080");
		AssertBrush(resources, CellPaletteInstaller.GridBackgroundBrushKey, "#FFFFFF");
		AssertBrush(resources, CellPaletteInstaller.HeaderForegroundBrushKey, "#000000");

		resources[CellPaletteInstaller.StatusBarPaddingKey].Should().Be(new Thickness(5));
		resources[CellPaletteInstaller.StatusBarItemSpacingKey].Should().Be(10d);
		resources[CellPaletteInstaller.StatusBarFontSizeKey].Should().Be(14d);
		resources[CellPaletteInstaller.ValidationPanelMaxHeightKey].Should().Be(100d);
		resources[CellPaletteInstaller.RowHeightKey].Should().Be(gridStyle.Layout.RowHeight);

		resources[CellPaletteInstaller.AppFontFamilyKey].Should().BeOfType<FontFamily>()
			.Which.Name.Should().Be("Arial");
		resources[CellPaletteInstaller.StatusBarFontWeightKey].Should().Be(FontWeight.Bold);
		resources[CellPaletteInstaller.StatusBarFontStyleKey].Should().Be(FontStyle.Italic);
		resources[CellPaletteInstaller.StatusBarTimerLabelFontSizeKey].Should().Be(16d);
		resources[CellPaletteInstaller.StatusBarTimerLabelFontWeightKey].Should().Be(FontWeight.Medium);
		resources[CellPaletteInstaller.StatusBarTimerLabelFontStyleKey].Should().Be(FontStyle.Italic);
		resources[CellPaletteInstaller.StatusBarTimerValueFontSizeKey].Should().Be(28d);
		resources[CellPaletteInstaller.StatusBarTimerValueFontWeightKey].Should().Be(FontWeight.SemiBold);
		resources[CellPaletteInstaller.StatusBarTimerValueFontStyleKey].Should().Be(FontStyle.Normal);

		resources.ContainsKey("StatusBarTimerFontSize").Should().BeFalse();

		resources.Count.Should().Be(58);
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
