using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using SemiStep.Core.Configuration;
using SemiStep.UI.MainWindow;
using SemiStep.UI.Styles;

namespace SemiStep.Tests.UI.MainWindow;

/// <summary>Hosts an <see cref="AppStatusBar"/> alone in a 1280 px headless window.</summary>
internal sealed class StatusBarWindow : IDisposable
{
	public const double WindowWidth = 1280;

	public StatusBarWindow(MainWindowViewModel viewModel, GridStyleOptions? gridStyle = null)
	{
		StatusBar = new AppStatusBar { DataContext = viewModel };
		Window = new Window { Width = WindowWidth, Height = 120, Content = StatusBar };
		CellPaletteInstaller.Install(Window.Resources, gridStyle ?? GridStyleOptions.Default);
		Window.Show();
		Settle();
	}

	public Window Window { get; }

	public AppStatusBar StatusBar { get; }

	public T Slot<T>(string name)
		where T : Control
	{
		return StatusBar.GetLogicalDescendants().OfType<T>().Single(control => control.Name == name);
	}

	public Rect BoundsInBar(string slotName)
	{
		var slot = Slot<Control>(slotName);
		var origin = slot.TranslatePoint(default, StatusBar)!.Value;

		return new Rect(origin, slot.Bounds.Size);
	}

	public void Click(string slotName)
	{
		var slot = Slot<Control>(slotName);
		var center = slot.TranslatePoint(new Point(slot.Bounds.Width / 2, slot.Bounds.Height / 2), Window)!.Value;
		Window.MouseDown(center, MouseButton.Left);
		Window.MouseUp(center, MouseButton.Left);
		Settle();
	}

	public void Settle()
	{
		Dispatcher.UIThread.RunJobs();
		Window.UpdateLayout();
	}

	public void Dispose()
	{
		Window.Close();
	}
}
