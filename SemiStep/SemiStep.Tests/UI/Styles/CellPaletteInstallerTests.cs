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
			GridLineColor = "#CCCCCC",
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
		AssertBrush(resources, CellPaletteInstaller.GridLineBrushKey, "#CCCCCC");

		resources.Count.Should().Be(24);
	}

	private static void AssertBrush(IResourceDictionary resources, string key, string expectedHex)
	{
		resources.ContainsKey(key).Should().BeTrue($"resource key '{key}' must be present");
		var brush = resources[key].Should().BeOfType<SolidColorBrush>().Subject;
		brush.Color.Should().Be(Color.Parse(expectedHex));
	}
}
