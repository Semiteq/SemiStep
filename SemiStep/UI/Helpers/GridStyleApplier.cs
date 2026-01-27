using Avalonia.Controls;

using Shared.Entities;

using UI.Controls;

namespace UI.Helpers;

public sealed class GridStyleApplier
{
	public void ApplyGridStyles(DataGrid grid, GridStyleOptions styles)
	{
		grid.RowHeight = styles.RowHeight;
		grid.GridLinesVisibility = DataGridGridLinesVisibility.All;
		grid.HorizontalGridLinesBrush = StyleHelper.ToBrush(styles.GridLineColor);
		grid.VerticalGridLinesBrush = StyleHelper.ToBrush(styles.GridLineColor);
	}

	public void ApplyControlStyles(
		ValidationErrorsPanel? validationPanel,
		AppStatusBar? statusBar,
		GridStyleOptions styles)
	{
		validationPanel?.ApplyStyles(styles);
		statusBar?.ApplyStyles(styles);
	}
}
