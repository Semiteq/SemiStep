using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

namespace SemiStep.Tests.UI.Helpers;

public static class ComboBoxTestHelper
{
	// The list items live in the popup's own top level, so the click goes there, not to the main window.
	public static void ClickListItem(ComboBox comboBox, int itemIndex)
	{
		var item = comboBox.ContainerFromIndex(itemIndex)!;
		var popupRoot = TopLevel.GetTopLevel(item)!;
		var point = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), popupRoot)!.Value;
		popupRoot.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
		popupRoot.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();
	}
}
