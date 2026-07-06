using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SemiStep.UI.StyleEditor;

public partial class RestartPromptDialog : Window
{
	public RestartPromptDialog()
	{
		InitializeComponent();
	}

	private void OnExitNowClick(object? sender, RoutedEventArgs e)
	{
		Close(true);
	}

	private void OnRestartLaterClick(object? sender, RoutedEventArgs e)
	{
		Close(false);
	}
}
