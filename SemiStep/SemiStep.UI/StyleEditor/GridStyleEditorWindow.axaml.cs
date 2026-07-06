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

	private async void OnSaveCompleted(bool saved)
	{
		if (!saved)
		{
			return;
		}

		var dialog = new RestartPromptDialog();
		var exitRequested = await dialog.ShowDialog<bool>(this);
		CompleteEditorClose(exitRequested);
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
