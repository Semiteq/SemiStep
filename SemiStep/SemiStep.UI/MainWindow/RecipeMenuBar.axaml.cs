using Avalonia.Controls;
using Avalonia.Input;

namespace SemiStep.UI.MainWindow;

public partial class RecipeMenuBar : UserControl
{
	public RecipeMenuBar()
	{
		InitializeComponent();
	}

	/// <summary>The item whose gesture the key is, or null; the window system keeps Exit's Alt+F4.</summary>
	public MenuItem? ItemFor(KeyEventArgs key)
	{
		var items = AppMenu.Items.OfType<MenuItem>().SelectMany(menu => menu.Items.OfType<MenuItem>());
		if (WindowGestures.RedoAlternate.Matches(key))
		{
			return items.FirstOrDefault(item => WindowGestures.Redo.Equals(item.InputGesture));
		}

		return items.FirstOrDefault(item =>
			item.InputGesture is { } gesture && !gesture.Equals(WindowGestures.Exit) && gesture.Matches(key));
	}
}
