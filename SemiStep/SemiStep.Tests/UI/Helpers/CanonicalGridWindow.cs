using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

using SemiStep.UI.RecipeGrid;

namespace SemiStep.Tests.UI.Helpers;

/// <summary>A canonical grid view over the fixture's recipe, shown in a headless window.</summary>
public sealed class CanonicalGridWindow : IDisposable
{
	public CanonicalGridWindow(UIFixture fixture)
	{
		Surface = fixture.CreateCanonicalSurface();
		Surface.Initialize();

		var view = new CanonicalRecipeGridView { DataContext = Surface };
		Window = new Window { Width = 1600, Height = 600, Content = view };
		Window.Show();
		Dispatcher.UIThread.RunJobs();
		DataGrid = view.FindControl<DataGrid>("RecipeGrid")!;
	}

	public CanonicalRecipeGridSurface Surface { get; }

	public Window Window { get; }

	public DataGrid DataGrid { get; }

	public ComboBox ComboAt(int rowIndex, string columnKey)
	{
		var column = DataGrid.Columns.Single(c => Equals(c.Tag, columnKey));

		return (ComboBox)column.GetCellContent(Surface.RecipeRows[rowIndex]);
	}

	public Point CenterOf(Control control)
	{
		return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)!.Value;
	}

	public void Click(
		Control control,
		MouseButton button = MouseButton.Left,
		RawInputModifiers modifiers = RawInputModifiers.None)
	{
		var point = CenterOf(control);
		Window.MouseDown(point, button, modifiers);
		Window.MouseUp(point, button, modifiers);
		Dispatcher.UIThread.RunJobs();
	}

	public void PressKey(Key key, PhysicalKey physicalKey, RawInputModifiers modifiers = RawInputModifiers.None)
	{
		Window.KeyPress(key, modifiers, physicalKey, null);
		Dispatcher.UIThread.RunJobs();
	}

	public void TypeText(string text)
	{
		Window.KeyTextInput(text);
		Dispatcher.UIThread.RunJobs();
	}

	public void Wheel(Control control, double notches)
	{
		Window.MouseWheel(CenterOf(control), new Vector(0, notches));
		Dispatcher.UIThread.RunJobs();
	}

	public double VerticalScrollOffset()
	{
		return DataGrid.GetVisualDescendants()
			.OfType<ScrollBar>()
			.First(scrollBar => scrollBar.Orientation == Orientation.Vertical)
			.Value;
	}

	public object? FocusedElement()
	{
		return Window.FocusManager.GetFocusedElement();
	}

	public void Dispose()
	{
		Window.Close();
		Surface.Dispose();
	}
}
