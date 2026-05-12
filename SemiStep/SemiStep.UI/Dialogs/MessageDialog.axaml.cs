using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SemiStep.UI.Dialogs;

public partial class MessageDialog : Window
{
	public MessageDialog()
	{
		InitializeComponent();
	}

	public MessageDialog(string title, string message) : this()
	{
		Title = title;
		MessageText.Text = message;
	}

	private void OnOkClick(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
