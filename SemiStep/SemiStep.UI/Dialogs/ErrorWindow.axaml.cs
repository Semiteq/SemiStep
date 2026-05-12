using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SemiStep.UI.Dialogs;

public partial class ErrorWindow : Window
{
	public ErrorWindow()
	{
		InitializeComponent();
	}

	public ErrorWindow(IReadOnlyList<string> errors) : this()
	{
		ErrorList.ItemsSource = errors;
	}

	private void OnExitClick(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
