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
		var gridStyle = GridStyleOptions.Default with
		{
			ReadOnlyCellDepth0Color = "#D8D8D8",
			ReadOnlyCellDepth1Color = "#CCD5E0",
			ReadOnlyCellDepth2Color = "#B8C3D1",
			ReadOnlyCellDepth3Color = "#94A2B3",
			ReadOnlyCellDepth0PastColor = "#C8C8C8",
			ReadOnlyCellDepth1PastColor = "#BCC4CE",
			ReadOnlyCellDepth2PastColor = "#ACB7C2",
			ReadOnlyCellDepth3PastColor = "#8590A0",
			ReadOnlyCellSelectedColor = "#6B95C0",
			ReadOnlyCellForegroundColor = "#606060",
			DisabledCellDepth0Color = "#E0E0E0",
			DisabledCellDepth1Color = "#D5DEEA",
			DisabledCellDepth2Color = "#C2CEDB",
			DisabledCellDepth3Color = "#9DABBC",
			DisabledCellDepth0PastColor = "#D0D0D0",
			DisabledCellDepth1PastColor = "#C5CDD8",
			DisabledCellDepth2PastColor = "#B5C0CC",
			DisabledCellDepth3PastColor = "#909AAA",
			DisabledCellSelectedColor = "#89B4D7",
			DisabledCellForegroundColor = "#808080",
			SelectionBackgroundColor = "#CCE4F7",
			SelectionForegroundColor = "#202020",
			CellChangedColor = "#FFCC80",
			CellChangedSelectedColor = "#EFD3A4",
			GridLineColor = "#CCCCCC",
			StatusBarBackgroundColor = "#F0F0F0",
			StatusBarForegroundColor = "#111111",
			StatusBarPadding = 5,
			StatusBarItemSpacing = 10,
			FontFamily = "Arial",
			StatusBarFontSize = 14,
			StatusBarFontWeight = 700,
			StatusBarItalic = true,
			StatusBarTimerLabelFontSize = 16,
			StatusBarTimerLabelFontWeight = 500,
			StatusBarTimerLabelItalic = true,
			StatusBarTimerValueFontSize = 28,
			StatusBarTimerValueFontWeight = 600,
			StatusBarTimerValueItalic = false,
			CellFontSize = 12,
			ValidationPanelBackgroundColor = "#FBFBFB",
			ValidationPanelForegroundColor = "#222222",
			ValidationPanelErrorColor = "#D32F2F",
			ValidationPanelWarningColor = "#F57C00",
			ValidationPanelMaxHeight = 100,
			InfoColor = "#1976D2",
			ConnectedColor = "#44BB44",
			DisconnectedColor = "#FF4444",
			LocalModeColor = "#6C707E",
			ConnectingColor = "#FFAF0F",
			PanelBackgroundColor = "#F8F8F8",
			PanelHeaderBackgroundColor = "#EEEEEE",
			SubtleBorderColor = "#D0D0D0",
			SeparatorColor = "#C0C0C0",
			SecondaryForegroundColor = "#888888",
			GridBorderColor = "#808080",
			GridBackgroundColor = "#FFFFFF",
			HeaderForegroundColor = "#000000",
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
		resources[CellPaletteInstaller.RowHeightKey].Should().Be(gridStyle.RowHeight);

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
		var gridStyle = GridStyleOptions.Default with { FontFamily = "" };
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
