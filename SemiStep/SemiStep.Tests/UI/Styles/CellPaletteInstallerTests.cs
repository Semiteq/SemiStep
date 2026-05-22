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
	public void Install_PopulatesFourBrushKeys_WithExpectedColors()
	{
		var gridStyle = GridStyleOptions.Default with
		{
			DisabledCellNormalColor = "#E0E0E0",
			DisabledCellSelectedBackgroundColor = "#89B4D7",
			DisabledCellForegroundColor = "#808080",
			GridLineColor = "#CCCCCC",
		};
		var resources = new ResourceDictionary();

		CellPaletteInstaller.Install(resources, gridStyle);

		AssertBrush(resources, CellPaletteInstaller.CellDisabledBackgroundBrushKey, "#E0E0E0");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledSelectedBackgroundBrushKey, "#89B4D7");
		AssertBrush(resources, CellPaletteInstaller.CellDisabledForegroundBrushKey, "#808080");
		AssertBrush(resources, CellPaletteInstaller.GridLineBrushKey, "#CCCCCC");

		resources.Count.Should().Be(4);
	}

	private static void AssertBrush(IResourceDictionary resources, string key, string expectedHex)
	{
		resources.ContainsKey(key).Should().BeTrue($"resource key '{key}' must be present");
		var brush = resources[key].Should().BeOfType<SolidColorBrush>().Subject;
		brush.Color.Should().Be(Color.Parse(expectedHex));
	}
}
