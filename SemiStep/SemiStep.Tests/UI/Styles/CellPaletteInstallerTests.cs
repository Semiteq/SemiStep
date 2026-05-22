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
	public void Install_PopulatesElevenBrushKeys_WithExpectedColors()
	{
		var gridStyle = GridStyleOptions.Default with
		{
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
			GridLineColor = "#CCCCCC",
		};
		var resources = new ResourceDictionary();

		CellPaletteInstaller.Install(resources, gridStyle);

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
		AssertBrush(resources, CellPaletteInstaller.GridLineBrushKey, "#CCCCCC");

		resources.Count.Should().Be(11);
	}

	private static void AssertBrush(IResourceDictionary resources, string key, string expectedHex)
	{
		resources.ContainsKey(key).Should().BeTrue($"resource key '{key}' must be present");
		var brush = resources[key].Should().BeOfType<SolidColorBrush>().Subject;
		brush.Color.Should().Be(Color.Parse(expectedHex));
	}
}
