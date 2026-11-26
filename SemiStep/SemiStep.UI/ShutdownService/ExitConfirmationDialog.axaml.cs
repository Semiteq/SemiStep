using Avalonia.Controls;
using Avalonia.Interactivity;

using SemiStep.UI.RecipeFile;

namespace SemiStep.UI.ShutdownService;

public partial class ExitConfirmationDialog : Window
{
	public ExitConfirmationDialog()
	{
		InitializeComponent();
	}

	private void OnSaveClick(object? sender, RoutedEventArgs e)
	{
		Close(UnsavedChangesChoice.Save);
	}

	private void OnDontSaveClick(object? sender, RoutedEventArgs e)
	{
		Close(UnsavedChangesChoice.DontSave);
	}

	private void OnCancelClick(object? sender, RoutedEventArgs e)
	{
		Close(UnsavedChangesChoice.Cancel);
	}
}
