using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SemiStep.UI.Plc;

internal partial class PlcConflictDialog : Window
{
	public bool KeepLocal { get; private set; }

	public bool Confirmed { get; private set; }

	internal PlcConflictDialog()
	{
		InitializeComponent();
	}

	private void OnKeepLocalClick(object? sender, RoutedEventArgs e)
	{
		KeepLocal = true;
		Confirmed = true;
		Close();
	}

	private void OnLoadFromPlcClick(object? sender, RoutedEventArgs e)
	{
		KeepLocal = false;
		Confirmed = true;
		Close();
	}
}
