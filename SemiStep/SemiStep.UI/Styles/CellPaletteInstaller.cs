using Avalonia.Controls;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Styles;

internal static class CellPaletteInstaller
{
	public const string CellDisabledDepth0BrushKey = "CellDisabledDepth0Brush";
	public const string CellDisabledDepth1BrushKey = "CellDisabledDepth1Brush";
	public const string CellDisabledDepth2BrushKey = "CellDisabledDepth2Brush";
	public const string CellDisabledDepth3BrushKey = "CellDisabledDepth3Brush";
	public const string CellDisabledDepth0PastBrushKey = "CellDisabledDepth0PastBrush";
	public const string CellDisabledDepth1PastBrushKey = "CellDisabledDepth1PastBrush";
	public const string CellDisabledDepth2PastBrushKey = "CellDisabledDepth2PastBrush";
	public const string CellDisabledDepth3PastBrushKey = "CellDisabledDepth3PastBrush";
	public const string CellDisabledSelectedBackgroundBrushKey = "CellDisabledSelectedBackgroundBrush";
	public const string CellDisabledForegroundBrushKey = "CellDisabledForegroundBrush";
	public const string GridLineBrushKey = "GridLineBrush";

	public static void Install(IResourceDictionary resources, GridStyleOptions gridStyle)
	{
		ArgumentNullException.ThrowIfNull(resources);
		ArgumentNullException.ThrowIfNull(gridStyle);

		resources[CellDisabledDepth0BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth0Color);
		resources[CellDisabledDepth1BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth1Color);
		resources[CellDisabledDepth2BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth2Color);
		resources[CellDisabledDepth3BrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth3Color);
		resources[CellDisabledDepth0PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth0PastColor);
		resources[CellDisabledDepth1PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth1PastColor);
		resources[CellDisabledDepth2PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth2PastColor);
		resources[CellDisabledDepth3PastBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellDepth3PastColor);
		resources[CellDisabledSelectedBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellSelectedColor);
		resources[CellDisabledForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellForegroundColor);
		resources[GridLineBrushKey] = PaletteBrushFactory.From(gridStyle.GridLineColor);
	}
}
