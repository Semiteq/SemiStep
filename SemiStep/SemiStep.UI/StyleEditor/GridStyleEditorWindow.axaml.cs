using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

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
		await dialog.ShowDialog(this);
		Close();
	}

	private void OnCancelClick(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
