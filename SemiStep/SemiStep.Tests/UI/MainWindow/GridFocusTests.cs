using System.Reactive.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "GridFocus")]
[Trait("Category", "Integration")]
public sealed class GridFocusTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private MainWindowView? _window;
	private string? _recipePath;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(3);
		// A clean seed lets New and Open run without the unsaved-changes prompt.
		_fixture.Session.MarkSaved();
		_viewModel = _fixture.CreateMainWindowViewModel();
		_viewModel.RecipeGrid.Initialize();
	}

	public ValueTask DisposeAsync()
	{
		_window?.Close();
		_viewModel.Dispose();
		if (_recipePath is not null)
		{
			File.Delete(_recipePath);
		}

		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void WindowOpen_FocusIsInTheGrid()
	{
		ShowWindow();

		FocusIsInside<DataGrid>().Should().BeTrue("the grid holds focus when the window opens");

		var selectionBefore = _viewModel.RecipeGrid.SelectedStepIndices.ToArray();
		_window!.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();
		_viewModel.RecipeGrid.SelectedStepIndices.Should().NotEqual(selectionBefore, "the arrow moves without a click");
	}

	[AvaloniaFact]
	public void OrientationFlip_FocusIsInTheTransposedGrid()
	{
		ShowWindow();
		MoveFocusOutOfTheGrid();

		_viewModel.ToggleOrientationCommand.Execute().Subscribe();
		Dispatcher.UIThread.RunJobs();

		FocusIsInside<TransposedStepListBox>().Should().BeTrue("the flipped-in grid takes focus");
	}

	[AvaloniaFact]
	public void NewRecipe_FocusIsInTheGrid()
	{
		ShowWindow();
		MoveFocusOutOfTheGrid();

		_viewModel.RecipeFile.NewRecipeCommand.Execute().Subscribe();
		Dispatcher.UIThread.RunJobs();

		FocusIsInside<DataGrid>().Should().BeTrue("the grid takes focus after a new recipe");
	}

	// A new recipe has no steps, so the transposed grid has no column to focus until the first step is added.
	[AvaloniaFact]
	public void NewRecipe_InTransposedView_AddedStepsAndArrowsStayInTheGrid()
	{
		ShowWindow();
		_viewModel.ToggleOrientationCommand.Execute().Subscribe();
		Dispatcher.UIThread.RunJobs();

		var window = _window!;
		window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control | RawInputModifiers.Shift);
		Dispatcher.UIThread.RunJobs();
		for (var i = 0; i < 3; i++)
		{
			window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);
			Dispatcher.UIThread.RunJobs();
		}

		window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
		window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(3, "precondition: New, then three added steps");
		FocusIsInside<TransposedStepListBox>().Should().BeTrue("the grid keeps focus after a new recipe");
	}

	[AvaloniaFact]
	public async Task OpenRecipe_FocusIsInTheGrid()
	{
		ShowWindow();
		_recipePath = Path.Combine(Path.GetTempPath(), $"semistep-focus-{Guid.NewGuid():N}.csv");
		_viewModel.RecipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(_recipePath));
		_viewModel.RecipeFile.OpenFileInteraction.RegisterHandler(context => context.SetOutput(_recipePath));
		await _viewModel.RecipeFile.SaveAsRecipeCommand.Execute();
		MoveFocusOutOfTheGrid();

		await _viewModel.RecipeFile.LoadRecipeCommand.Execute();
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(3, "precondition: the saved recipe loaded");
		FocusIsInside<DataGrid>().Should().BeTrue("the grid takes focus after a recipe opens");
	}

	private void ShowWindow()
	{
		_window = new MainWindowView { ViewModel = _viewModel };
		_window.Show();
		Dispatcher.UIThread.RunJobs();
	}

	private void MoveFocusOutOfTheGrid()
	{
		var window = _window!;
		var grid = window.GetVisualDescendants().OfType<DataGrid>().Single();
		window.GetVisualDescendants()
			.OfType<InputElement>()
			.First(element => element.Focusable
				&& element.IsEffectivelyVisible
				&& element.IsEffectivelyEnabled
				&& !grid.IsVisualAncestorOf(element))
			.Focus();
		Dispatcher.UIThread.RunJobs();
		FocusIsInside<DataGrid>().Should().BeFalse("precondition: focus left the grid");
	}

	private bool FocusIsInside<TGrid>()
		where TGrid : Control
	{
		var window = _window!;
		var grid = window.GetVisualDescendants().OfType<TGrid>().Single();

		return window.FocusManager?.GetFocusedElement() is Visual focused
			&& (ReferenceEquals(focused, grid) || grid.IsVisualAncestorOf(focused));
	}
}
