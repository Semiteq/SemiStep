using Avalonia.Controls;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Styles;

internal static class CellPaletteInstaller
{
	public const string CellDisabledBackgroundBrushKey = "CellDisabledBackgroundBrush";
	public const string CellDisabledSelectedBackgroundBrushKey = "CellDisabledSelectedBackgroundBrush";
	public const string CellDisabledForegroundBrushKey = "CellDisabledForegroundBrush";
	public const string GridLineBrushKey = "GridLineBrush";

	public static void Install(IResourceDictionary resources, GridStyleOptions gridStyle)
	{
		ArgumentNullException.ThrowIfNull(resources);
		ArgumentNullException.ThrowIfNull(gridStyle);

		resources[CellDisabledBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellNormalColor);
		resources[CellDisabledSelectedBackgroundBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellSelectedBackgroundColor);
		resources[CellDisabledForegroundBrushKey] = PaletteBrushFactory.From(gridStyle.DisabledCellForegroundColor);
		resources[GridLineBrushKey] = PaletteBrushFactory.From(gridStyle.GridLineColor);
	}
}
