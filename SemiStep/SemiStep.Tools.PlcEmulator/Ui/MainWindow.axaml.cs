using Avalonia.Controls;

namespace SemiStep.Tools.PlcEmulator.Ui;

internal sealed partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
	}

	private void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
	{
		if (e.Row.DataContext is MemoryRowViewModel row)
		{
			row.IsEditing = true;
		}
	}

	private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
	{
		if (e.Row.DataContext is not MemoryRowViewModel row)
		{
			return;
		}

		row.IsEditing = false;
		var isCommit = e.EditAction == DataGridEditAction.Commit;
		if (isCommit && e.EditingElement is TextBox editor && DataContext is MainWindowViewModel viewModel)
		{
			viewModel.Edit(row, editor.Text ?? string.Empty);
		}
	}
}
