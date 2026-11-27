using Avalonia.Interactivity;

using ReactiveUI.Avalonia;

namespace SemiStep.UI.StyleEditor;

internal partial class GridStyleEditorWindow : ReactiveWindow<GridStyleEditorViewModel>
{
	public GridStyleEditorWindow()
	{
		InitializeComponent();
	}

	private void OnCloseClick(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
