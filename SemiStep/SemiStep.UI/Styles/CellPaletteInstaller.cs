using Avalonia.Controls;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Styles;

internal static class CellPaletteInstaller
{
	public const string CellReadOnlyDepth0BrushKey = "CellReadOnlyDepth0Brush";
	public const string CellReadOnlyDepth1BrushKey = "CellReadOnlyDepth1Brush";
	public const string CellReadOnlyDepth2BrushKey = "CellReadOnlyDepth2Brush";
	public const string CellReadOnlyDepth3BrushKey = "CellReadOnlyDepth3Brush";
	public const string CellReadOnlyDepth0PastBrushKey = "CellReadOnlyDepth0PastBrush";
	public const string CellReadOnlyDepth1PastBrushKey = "CellReadOnlyDepth1PastBrush";
	public const string CellReadOnlyDepth2PastBrushKey = "CellReadOnlyDepth2PastBrush";
	public const string CellReadOnlyDepth3PastBrushKey = "CellReadOnlyDepth3PastBrush";
	public const string CellReadOnlySelectedBackgroundBrushKey = "CellReadOnlySelectedBackgroundBrush";
	public const string CellReadOnlyForegroundBrushKey = "CellReadOnlyForegroundBrush";
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
	public const string SelectionBackgroundBrushKey = "SelectionBackgroundBrush";
	public const string SelectionForegroundBrushKey = "SelectionForegroundBrush";
	public const string CellChangedBrushKey = "CellChangedBrush";
	public const string GridLineBrushKey = "GridLineBrush";

	public static void Install(IResourceDictionary resources, GridStyleOptions gridStyle)
	{
		ArgumentNullException.ThrowIfNull(resources);
		ArgumentNullException.ThrowIfNull(gridStyle);

		resources[CellReadOnlyDepth0BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth0Color);
		resources[CellReadOnlyDepth1BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth1Color);
		resources[CellReadOnlyDepth2BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth2Color);
		resources[CellReadOnlyDepth3BrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth3Color);
		resources[CellReadOnlyDepth0PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth0PastColor);
		resources[CellReadOnlyDepth1PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth1PastColor);
		resources[CellReadOnlyDepth2PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth2PastColor);
		resources[CellReadOnlyDepth3PastBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellDepth3PastColor);
		resources[CellReadOnlySelectedBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellSelectedColor);
		resources[CellReadOnlyForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.ReadOnlyCellForegroundColor);
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
		resources[SelectionBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.SelectionBackgroundColor);
		resources[SelectionForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.SelectionForegroundColor);
		resources[CellChangedBrushKey] = PaletteBrushFactory.From(gridStyle.CellChangedColor);
		resources[GridLineBrushKey] = PaletteBrushFactory.From(gridStyle.GridLineColor);
	}
}
