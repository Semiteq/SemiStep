using Avalonia.Controls;
using Avalonia.Threading;

namespace SemiStep.Tests.UI.Helpers;

public static class DataGridTestHelper
{
	public static void SetCurrentCell(DataGrid dataGrid, int rowIndex, DataGridColumn column)
	{
		dataGrid.Focus();
		dataGrid.SelectedIndex = rowIndex;
		Dispatcher.UIThread.RunJobs();

		dataGrid.CurrentColumn = column;
		Dispatcher.UIThread.RunJobs();
	}
}
