using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UI.Windows;

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
