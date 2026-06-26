using Avalonia.Controls;
using Avalonia.Interactivity;

using SemiStep.UI.ShutdownService;

namespace SemiStep.UI.StyleEditor;

public partial class RestartPromptDialog : Window
{
	public RestartPromptDialog()
	{
		InitializeComponent();
	}

	private void OnExitNowClick(object? sender, RoutedEventArgs e)
	{
		Close();
		DesktopShutdownService.Shutdown();
	}

	private void OnRestartLaterClick(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
