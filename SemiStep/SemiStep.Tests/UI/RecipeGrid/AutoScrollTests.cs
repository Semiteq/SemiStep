using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.RecipeGrid.Transposed;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// Auto-scroll follows the running step and pauses 10 s after operator scroll input. The pause runs on the
// fixture's virtual scheduler, so the tests advance time instead of waiting.
[Trait("Component", "UI")]
[Trait("Area", "AutoScroll")]
[Trait("Category", "Integration")]
public sealed class AutoScrollTests : IAsyncLifetime
{
	private const int StepCount = 80;
	private const int FarStep = 70;
	private const int MiddleStep = 40;

	private readonly UIFixture _fixture = new();
	private Window? _window;
	private CanonicalRecipeGridSurface _surface = null!;
	private DataGrid _dataGrid = null!;
	private TransposedRecipeGridSurface _transposed = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(StepCount);
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void LineChange_ScrollsTheRunningRowIntoView()
	{
		ShowCanonical();

		Run(FarStep);

		RealizedRows().Should().Contain(FarStep);
	}

	[AvaloniaFact]
	public void Wheel_PausesFollowing_UntilTenSecondsPass()
	{
		ShowCanonical();
		Run(0);

		_window!.MouseWheel(CenterOf(_dataGrid), new Vector(0, -1));
		Run(FarStep);

		RealizedRows().Should().NotContain(FarStep, "a line change during the pause does not scroll");

		_fixture.Scheduler.AdvanceBy(TimeSpan.FromSeconds(10));
		Dispatcher.UIThread.RunJobs();

		RealizedRows().Should().Contain(FarStep, "the grid returns to the running step when the pause ends");
	}

	[AvaloniaFact]
	public void PageDownKey_PausesFollowing()
	{
		ShowCanonical();
		Run(0);
		_dataGrid.Focus();

		_window!.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
		Dispatcher.UIThread.RunJobs();
		var realizedAfterKey = RealizedRows();
		Run(FarStep);

		RealizedRows().Should().Equal(realizedAfterKey);
	}

	[AvaloniaFact]
	public void ScrollBarDrag_PausesFollowing()
	{
		ShowCanonical();
		Run(0);
		var thumb = _dataGrid.GetVisualDescendants().OfType<ScrollBar>()
			.First(bar => bar.Orientation == Orientation.Vertical)
			.GetVisualDescendants().OfType<Thumb>().First();

		thumb.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = new Vector(0, 1) });
		Run(FarStep);

		RealizedRows().Should().NotContain(FarStep);
	}

	[AvaloniaFact]
	public void EachInput_RestartsTheTenSeconds()
	{
		ShowCanonical();
		Run(0);
		_window!.MouseWheel(CenterOf(_dataGrid), new Vector(0, -1));
		_fixture.Scheduler.AdvanceBy(TimeSpan.FromSeconds(6));

		_window!.MouseWheel(CenterOf(_dataGrid), new Vector(0, -1));
		Run(FarStep);
		_fixture.Scheduler.AdvanceBy(TimeSpan.FromSeconds(6));
		Dispatcher.UIThread.RunJobs();

		RealizedRows().Should().NotContain(FarStep);
	}

	[AvaloniaFact]
	public void Following_AfterThePause_KeepsTheHorizontalOffset()
	{
		ShowCanonical(width: 250);
		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();
		_dataGrid.CurrentColumn = _dataGrid.Columns[1];
		Run(0);
		_window!.MouseWheel(CenterOf(_dataGrid), new Vector(0, -3), RawInputModifiers.Shift);
		Dispatcher.UIThread.RunJobs();
		var offsetAfterScroll = CanonicalHorizontalOffset();
		offsetAfterScroll.Should().BeGreaterThan(0, "the check needs the grid scrolled right");

		_fixture.Scheduler.AdvanceBy(TimeSpan.FromSeconds(10));
		Dispatcher.UIThread.RunJobs();
		Run(FarStep);

		RealizedRows().Should().Contain(FarStep, "following did scroll");
		CanonicalHorizontalOffset().Should().Be(offsetAfterScroll, "following moves the view vertically only");
	}

	[AvaloniaFact]
	public void CodeDrivenScroll_DoesNotPause()
	{
		ShowCanonical();
		Run(0);

		_dataGrid.ScrollIntoView(_surface.RecipeRows[40], null);
		Dispatcher.UIThread.RunJobs();
		Run(FarStep);

		RealizedRows().Should().Contain(FarStep);
	}

	[AvaloniaFact]
	public void Following_NeverMovesTheSelectionTheCurrentCellOrTheFocus()
	{
		ShowCanonical();
		_surface.RequestSelection(2);
		Dispatcher.UIThread.RunJobs();
		_dataGrid.CurrentColumn = _dataGrid.Columns[1];
		_dataGrid.Focus();
		Dispatcher.UIThread.RunJobs();
		var currentColumn = _dataGrid.CurrentColumn;
		var focused = FocusedElement();
		currentColumn.Should().NotBeNull("the check needs a current cell to watch");
		focused.Should().NotBeNull("the check needs a focused element to watch");

		Run(FarStep);
		_window!.MouseWheel(CenterOf(_dataGrid), new Vector(0, -1));
		Run(FarStep - 1);
		_fixture.Scheduler.AdvanceBy(TimeSpan.FromSeconds(10));
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(2);
		_dataGrid.SelectedIndex.Should().Be(2);
		_dataGrid.CurrentColumn.Should().BeSameAs(currentColumn);
		FocusedElement().Should().BeSameAs(focused);
	}

	[AvaloniaFact]
	public void RunEndingDuringThePause_ScrollsNothing()
	{
		ShowCanonical();
		Run(0);
		_window!.MouseWheel(CenterOf(_dataGrid), new Vector(0, -1));
		Run(FarStep);
		var realized = RealizedRows();

		_fixture.S7Service.PushExecutionState(PlcExecutionInfo.Empty);
		Dispatcher.UIThread.RunJobs();
		_fixture.Scheduler.AdvanceBy(TimeSpan.FromSeconds(10));
		Dispatcher.UIThread.RunJobs();

		RealizedRows().Should().Equal(realized);
	}

	[AvaloniaFact]
	public void Transposed_LineChange_ScrollsTheRunningColumnIntoView()
	{
		var transposed = _fixture.CreateTransposedSurface();
		transposed.Initialize();
		var view = new TransposedRecipeGridView { DataContext = transposed };
		var stepListBox = view.FindControl<ListBox>("StepListBox")!;
		stepListBox.UseTransposedColumnsPanel();
		Show(view, width: 800);

		Run(FarStep);

		stepListBox.ContainerFromIndex(FarStep).Should().NotBeNull("the running column is realised in view");
		stepListBox.SelectedIndex.Should().Be(-1, "following does not select");
	}

	[AvaloniaFact]
	public void Transposed_Wheel_PausesFollowing_UntilTenSecondsPass()
	{
		var stepListBox = ShowTransposed();
		Run(0);

		_window!.MouseWheel(CenterOf(stepListBox), new Vector(0, -1));
		Run(FarStep);

		stepListBox.ContainerFromIndex(FarStep).Should().BeNull("a line change during the pause does not scroll");

		_fixture.Scheduler.AdvanceBy(TimeSpan.FromSeconds(10));
		Dispatcher.UIThread.RunJobs();

		stepListBox.ContainerFromIndex(FarStep).Should().NotBeNull("the grid returns to the running step");
	}

	[AvaloniaTheory]
	[InlineData(Key.Up, PhysicalKey.ArrowUp)]
	[InlineData(Key.Down, PhysicalKey.ArrowDown)]
	[InlineData(Key.PageUp, PhysicalKey.PageUp)]
	[InlineData(Key.PageDown, PhysicalKey.PageDown)]
	[InlineData(Key.Home, PhysicalKey.Home)]
	[InlineData(Key.End, PhysicalKey.End)]
	[InlineData(Key.Left, PhysicalKey.ArrowLeft)]
	[InlineData(Key.Right, PhysicalKey.ArrowRight)]
	public void Transposed_NavigationKey_PausesFollowing(Key key, PhysicalKey physicalKey)
	{
		var stepListBox = ShowTransposed();
		Run(MiddleStep);
		stepListBox.ContainerFromIndex(MiddleStep)!.Focus();
		Dispatcher.UIThread.RunJobs();

		_window!.KeyPress(key, RawInputModifiers.None, physicalKey, null);
		Dispatcher.UIThread.RunJobs();
		var offsetAfterKey = HorizontalOffset(stepListBox);
		Run(FarthestStepOutOfView(stepListBox));

		HorizontalOffset(stepListBox).Should().Be(offsetAfterKey, "the key paused following");
	}

	[AvaloniaFact]
	public void Transposed_ScrollBarDrag_PausesFollowing()
	{
		var stepListBox = ShowTransposed();
		Run(0);
		var thumb = stepListBox.GetVisualDescendants().OfType<ScrollBar>()
			.First(bar => bar.Orientation == Orientation.Horizontal)
			.GetVisualDescendants().OfType<Thumb>().First();

		thumb.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = new Vector(1, 0) });
		Run(FarStep);

		stepListBox.ContainerFromIndex(FarStep).Should().BeNull();
	}

	[AvaloniaFact]
	public void Transposed_Following_NeverMovesTheSelectionOrTheFocus()
	{
		var stepListBox = ShowTransposed();
		_transposed.RequestSelection(2);
		Dispatcher.UIThread.RunJobs();
		stepListBox.ContainerFromIndex(2)!.Focus();
		Dispatcher.UIThread.RunJobs();
		var focused = FocusedElement();
		focused.Should().NotBeNull("the check needs a focused element to watch");

		Run(FarStep);
		_window!.MouseWheel(CenterOf(stepListBox), new Vector(0, -1));
		Run(FarStep - 1);
		_fixture.Scheduler.AdvanceBy(TimeSpan.FromSeconds(10));
		Dispatcher.UIThread.RunJobs();

		stepListBox.ContainerFromIndex(FarStep - 1).Should().NotBeNull("following did scroll");
		_transposed.SelectedStepIndices.Should().Equal(2);
		stepListBox.SelectedIndex.Should().Be(2);
		FocusedElement().Should().BeSameAs(focused);
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void OrientationFlipMidRun_ScrollsTheIncomingViewToTheRunningStep(bool startTransposed)
	{
		var router = _fixture.CreateActiveSurface();
		router.Initialize();
		if (startTransposed)
		{
			router.ToggleOrientation();
		}

		var host = new RecipeGridHost { DataContext = router };
		Show(host, width: 800);
		Run(FarStep);

		router.ToggleOrientation();
		Dispatcher.UIThread.RunJobs();

		if (startTransposed)
		{
			_surface = router.CanonicalSurface;
			_dataGrid = host.GetVisualDescendants().OfType<DataGrid>().Single();
			RealizedRows().Should().Contain(FarStep, "the canonical view shows the running row after the flip");
		}
		else
		{
			var stepListBox = host.GetVisualDescendants().OfType<ListBox>().Single(box => box.Name == "StepListBox");
			stepListBox.ContainerFromIndex(FarStep).Should().NotBeNull("the transposed view shows the running column after the flip");
		}
	}

	[AvaloniaFact]
	public void RunEndedBeforeTheFlip_ScrollsNothing()
	{
		var router = _fixture.CreateActiveSurface();
		router.Initialize();
		var host = new RecipeGridHost { DataContext = router };
		Show(host, width: 800);
		Run(FarStep);
		_fixture.S7Service.PushExecutionState(PlcExecutionInfo.Empty);
		Dispatcher.UIThread.RunJobs();

		router.ToggleOrientation();
		Dispatcher.UIThread.RunJobs();

		var stepListBox = host.GetVisualDescendants().OfType<ListBox>().Single(box => box.Name == "StepListBox");
		stepListBox.ContainerFromIndex(FarStep).Should().BeNull("no run is active, so the incoming view stays at its start");
	}

	private ListBox ShowTransposed()
	{
		_transposed = _fixture.CreateTransposedSurface();
		_transposed.Initialize();
		var view = new TransposedRecipeGridView { DataContext = _transposed };
		var stepListBox = view.FindControl<ListBox>("StepListBox")!;
		stepListBox.UseTransposedColumnsPanel();
		Show(view, width: 800);

		return stepListBox;
	}

	private double CanonicalHorizontalOffset()
	{
		return _dataGrid.GetVisualDescendants().OfType<ScrollBar>()
			.First(bar => bar.Orientation == Orientation.Horizontal)
			.Value;
	}

	private static double HorizontalOffset(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First().Offset.X;
	}

	// The end of the recipe the view is farther from, so following there would move the view.
	private static int FarthestStepOutOfView(ListBox stepListBox)
	{
		return stepListBox.ContainerFromIndex(StepCount - 1) is null ? StepCount - 1 : 0;
	}

	private IInputElement? FocusedElement()
	{
		return _window!.FocusManager!.GetFocusedElement();
	}

	private void ShowCanonical(double width = 1200)
	{
		_surface = _fixture.CreateCanonicalSurface();
		_surface.Initialize();
		var view = new CanonicalRecipeGridView { DataContext = _surface };
		Show(view, width);
		_dataGrid = view.FindControl<DataGrid>("RecipeGrid")!;
	}

	private void Show(Control view, double width)
	{
		_window = new Window { Width = width, Height = 400 };
		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		_window.Content = view;
		_window.Show();
		Dispatcher.UIThread.RunJobs();
	}

	private void Run(int line)
	{
		_fixture.S7Service.PushExecutionState(PlcExecutionInfo.Empty with { RecipeActive = true, ActualLine = line });
		Dispatcher.UIThread.RunJobs();
	}

	private Point CenterOf(Control control)
	{
		return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), _window!)!.Value;
	}

	private List<int> RealizedRows()
	{
		return [.. _dataGrid.GetVisualDescendants()
			.OfType<DataGridRow>()
			.Where(row => row.IsVisible)
			.Select(row => row.DataContext)
			.OfType<RecipeRowViewModel>()
			.Select(_surface.RecipeRows.IndexOf)
			.Where(index => index >= 0)
			.Order()];
	}
}
