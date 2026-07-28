using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

using Avalonia.Controls;
using Avalonia.Interactivity;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace SemiStep.UI.StyleEditor;

internal partial class GridStyleEditorWindow : ReactiveWindow<GridStyleEditorViewModel>
{
	public GridStyleEditorWindow()
	{
		InitializeComponent();

		this.WhenActivated(disposables =>
		{
			if (ViewModel is null)
			{
				return;
			}

			ViewModel.SaveCommand
				.Subscribe(saved => OnSaveCompleted(saved))
				.DisposeWith(disposables);
		});
	}

	internal async void OnSaveCompleted(bool saved)
	{
		if (!saved)
		{
			return;
		}

		try
		{
			var dialog = new RestartPromptDialog();
			var exitRequested = await dialog.ShowDialog<bool>(this);
			CompleteEditorClose(exitRequested);
		}
		catch (Exception ex)
		{
			// This is an async void subscription callback: an unhandled throw here unwinds the
			// dispatcher loop into Program.Main and kills the process. Surface on the editor's own
			// error surface (a modal's fault must not land behind the modal) and still close the
			// editor rather than leaving it hanging.
			ViewModel?.ReportSaveException(ex);
			CompleteEditorClose(false);
		}
	}

	// Captures the owner before Close(): Avalonia detaches the owned-window link during the close
	// sequence, so reading Owner afterwards yields null and silently drops the exit intent.
	internal void CompleteEditorClose(bool exitRequested)
	{
		var owner = Owner as Window;
		Close();

		if (exitRequested)
		{
			owner?.Close();
		}
	}

	private void OnCancelClick(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
