using Avalonia.Controls;
using Avalonia.Interactivity;

using UI.Models;

namespace UI.Views;

public partial class ExitConfirmationDialog : Window
{
	public ExitConfirmationDialog()
	{
		InitializeComponent();
	}

	private void OnSaveClick(object? sender, RoutedEventArgs e)
	{
		Close(ExitConfirmationResult.Save);
	}

	private void OnDontSaveClick(object? sender, RoutedEventArgs e)
	{
		Close(ExitConfirmationResult.DontSave);
	}

	private void OnCancelClick(object? sender, RoutedEventArgs e)
	{
		Close(ExitConfirmationResult.Cancel);
	}
}
