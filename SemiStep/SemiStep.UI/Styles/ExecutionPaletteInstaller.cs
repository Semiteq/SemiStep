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
		resources[ExecRowDepth0BrushKey] = PaletteBrushFactory.From(gridStyle.Execution.Depth0);
		resources[ExecRowDepth1BrushKey] = PaletteBrushFactory.From(gridStyle.Execution.Depth1);
		resources[ExecRowDepth2BrushKey] = PaletteBrushFactory.From(gridStyle.Execution.Depth2);
		resources[ExecRowDepth3BrushKey] = PaletteBrushFactory.From(gridStyle.Execution.Depth3);
		resources[ExecRowDepth0PastBrushKey] = PaletteBrushFactory.From(gridStyle.Execution.Depth0Past);
		resources[ExecRowDepth1PastBrushKey] = PaletteBrushFactory.From(gridStyle.Execution.Depth1Past);
		resources[ExecRowDepth2PastBrushKey] = PaletteBrushFactory.From(gridStyle.Execution.Depth2Past);
		resources[ExecRowDepth3PastBrushKey] = PaletteBrushFactory.From(gridStyle.Execution.Depth3Past);
		resources[CurrentStepMarkerBrushKey] = PaletteBrushFactory.From(gridStyle.Execution.CurrentStepMarker);
	}
}
