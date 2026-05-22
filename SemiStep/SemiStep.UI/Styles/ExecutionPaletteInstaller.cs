using Avalonia.Controls;
using Avalonia.Media;

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

		// `ExecRowDepth0Brush` (and its `Past` counterpart) represent the "outside any loop"
		// tint. No selector currently consumes them — the brush is reserved for future explicit
		// styling of non-loop rows. Keeping the resource installed lets us add that selector
		// without revisiting the configuration model.
		resources[ExecRowDepth0BrushKey] = CreateBrush(gridStyle.ExecutionDepth0Color);
		resources[ExecRowDepth1BrushKey] = CreateBrush(gridStyle.ExecutionDepth1Color);
		resources[ExecRowDepth2BrushKey] = CreateBrush(gridStyle.ExecutionDepth2Color);
		resources[ExecRowDepth3BrushKey] = CreateBrush(gridStyle.ExecutionDepth3Color);
		resources[ExecRowDepth0PastBrushKey] = CreateBrush(gridStyle.ExecutionDepth0PastColor);
		resources[ExecRowDepth1PastBrushKey] = CreateBrush(gridStyle.ExecutionDepth1PastColor);
		resources[ExecRowDepth2PastBrushKey] = CreateBrush(gridStyle.ExecutionDepth2PastColor);
		resources[ExecRowDepth3PastBrushKey] = CreateBrush(gridStyle.ExecutionDepth3PastColor);
		resources[CurrentStepMarkerBrushKey] = CreateBrush(gridStyle.ExecutionCurrentStepMarkerColor);
	}

	private static SolidColorBrush CreateBrush(string hex)
	{
		return new SolidColorBrush(Color.Parse(hex));
	}
}
