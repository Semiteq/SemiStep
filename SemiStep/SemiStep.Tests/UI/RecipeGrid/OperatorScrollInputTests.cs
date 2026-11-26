using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "AutoScroll")]
[Trait("Category", "Integration")]
public sealed class OperatorScrollInputTests : IDisposable
{
	private readonly Window _window = new() { Width = 400, Height = 300 };
	private readonly StackPanel _grid = new();
	private readonly TextBox _editor = new();
	private readonly Button _cell = new() { Content = "cell" };
	private int _reports;

	public OperatorScrollInputTests()
	{
		_grid.Children.Add(_editor);
		_grid.Children.Add(_cell);
		_window.Content = _grid;
	}

	public void Dispose()
	{
		_window.Close();
	}

	[AvaloniaTheory]
	[InlineData(Key.Home, PhysicalKey.Home)]
	[InlineData(Key.End, PhysicalKey.End)]
	[InlineData(Key.Up, PhysicalKey.ArrowUp)]
	[InlineData(Key.Down, PhysicalKey.ArrowDown)]
	public void KeyInsideAnOpenEditor_DoesNotReport(Key key, PhysicalKey physicalKey)
	{
		using var watch = ShowAndWatch();
		_editor.Focus();

		_window.KeyPress(key, RawInputModifiers.None, physicalKey, null);

		_reports.Should().Be(0, "the key moves the caret in the editor, not the view");
	}

	[AvaloniaFact]
	public void KeyOutsideAnEditor_Reports()
	{
		using var watch = ShowAndWatch();
		_cell.Focus();

		_window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);

		_reports.Should().Be(1);
	}

	[AvaloniaFact]
	public void ScrollBarCreatedAfterTheWatchStarts_ReportsAThumbDragAndATrackClick()
	{
		using var watch = ShowAndWatch();
		var scrollBar = new ScrollBar
		{
			Orientation = Orientation.Vertical,
			Height = 200,
			Maximum = 100,
			ViewportSize = 10,
			Visibility = ScrollBarVisibility.Visible,
		};
		_grid.Children.Add(scrollBar);
		Dispatcher.UIThread.RunJobs();

		var thumb = scrollBar.GetVisualDescendants().OfType<Thumb>().First();
		thumb.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = new Vector(0, 1) });
		var afterDrag = _reports;
		var trackButton = scrollBar.GetVisualDescendants().OfType<RepeatButton>().First();
		trackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

		afterDrag.Should().Be(1, "a thumb drag on a scroll bar made after the watch started is operator input");
		_reports.Should().Be(2, "a track click on that scroll bar is operator input too");
	}

	private IDisposable ShowAndWatch()
	{
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return OperatorScrollInput.Watch(_grid, () => _reports++, stepsRunHorizontally: false);
	}
}
