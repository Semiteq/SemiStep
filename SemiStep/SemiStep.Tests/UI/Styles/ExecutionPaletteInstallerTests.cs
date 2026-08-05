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
public sealed class ExecutionPaletteInstallerTests
{
	[AvaloniaFact]
	public void Install_PopulatesNineBrushKeys_WithExpectedColors()
	{
		var gridStyle = GridStyleOptions.Default with
		{
			Execution = new ExecutionPalette(
				Depth0: "#FFFFFF",
				Depth1: "#E8F3FF",
				Depth2: "#D0E7FF",
				Depth3: "#A8D0FF",
				Depth0Past: "#F0F0F0",
				Depth1Past: "#DCE5EE",
				Depth2Past: "#C4D2E0",
				Depth3Past: "#9CB4CC",
				CurrentStepMarker: "#FF8800"),
		};
		var resources = new ResourceDictionary();

		ExecutionPaletteInstaller.Install(resources, gridStyle);

		AssertBrush(resources, ExecutionPaletteInstaller.ExecRowDepth0BrushKey, "#FFFFFF");
		AssertBrush(resources, ExecutionPaletteInstaller.ExecRowDepth1BrushKey, "#E8F3FF");
		AssertBrush(resources, ExecutionPaletteInstaller.ExecRowDepth2BrushKey, "#D0E7FF");
		AssertBrush(resources, ExecutionPaletteInstaller.ExecRowDepth3BrushKey, "#A8D0FF");
		AssertBrush(resources, ExecutionPaletteInstaller.ExecRowDepth0PastBrushKey, "#F0F0F0");
		AssertBrush(resources, ExecutionPaletteInstaller.ExecRowDepth1PastBrushKey, "#DCE5EE");
		AssertBrush(resources, ExecutionPaletteInstaller.ExecRowDepth2PastBrushKey, "#C4D2E0");
		AssertBrush(resources, ExecutionPaletteInstaller.ExecRowDepth3PastBrushKey, "#9CB4CC");
		AssertBrush(resources, ExecutionPaletteInstaller.CurrentStepMarkerBrushKey, "#FF8800");

		resources.Count.Should().Be(9);
	}

	private static void AssertBrush(IResourceDictionary resources, string key, string expectedHex)
	{
		resources.ContainsKey(key).Should().BeTrue($"resource key '{key}' must be present");
		var brush = resources[key].Should().BeOfType<SolidColorBrush>().Subject;
		brush.Color.Should().Be(Color.Parse(expectedHex));
	}
}
