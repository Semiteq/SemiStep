using Avalonia.Controls;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Styles;

internal static class ExecutionPaletteInstaller
{
	public const string ExecRowDepth0BrushKey = "ExecRowDepth0Brush";
	public const string ExecRowDepth1BrushKey = "ExecRowDepth1Brush";
	public const string ExecRowDepth2BrushKey = "ExecRowDepth2Brush";
	public const string ExecRowDepth3BrushKey = "ExecRowDepth3Brush";
	public const string ExecRowDepth0PastBrushKey = "ExecRowDepth0PastBrush";
	public const string ExecRowDepth1PastBrushKey = "ExecRowDepth1PastBrush";
	public const string ExecRowDepth2PastBrushKey = "ExecRowDepth2PastBrush";
	public const string ExecRowDepth3PastBrushKey = "ExecRowDepth3PastBrush";
	public const string CurrentStepMarkerBrushKey = "CurrentStepMarkerBrush";

	public static void Install(IResourceDictionary resources, GridStyleOptions gridStyle)
	{
		ArgumentNullException.ThrowIfNull(resources);
		ArgumentNullException.ThrowIfNull(gridStyle);

		// Depth0 brushes (the "outside any loop" tint) are installed but not yet consumed by any selector.
		resources[ExecRowDepth0BrushKey] = PaletteBrushFactory.From(gridStyle.ExecutionDepth0Color);
		resources[ExecRowDepth1BrushKey] = PaletteBrushFactory.From(gridStyle.ExecutionDepth1Color);
		resources[ExecRowDepth2BrushKey] = PaletteBrushFactory.From(gridStyle.ExecutionDepth2Color);
		resources[ExecRowDepth3BrushKey] = PaletteBrushFactory.From(gridStyle.ExecutionDepth3Color);
		resources[ExecRowDepth0PastBrushKey] = PaletteBrushFactory.From(gridStyle.ExecutionDepth0PastColor);
		resources[ExecRowDepth1PastBrushKey] = PaletteBrushFactory.From(gridStyle.ExecutionDepth1PastColor);
		resources[ExecRowDepth2PastBrushKey] = PaletteBrushFactory.From(gridStyle.ExecutionDepth2PastColor);
		resources[ExecRowDepth3PastBrushKey] = PaletteBrushFactory.From(gridStyle.ExecutionDepth3PastColor);
		resources[CurrentStepMarkerBrushKey] = PaletteBrushFactory.From(gridStyle.ExecutionCurrentStepMarkerColor);
	}
}
