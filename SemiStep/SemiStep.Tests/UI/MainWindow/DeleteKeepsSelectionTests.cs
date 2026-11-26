using System.Globalization;
using System.Reactive.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "GridFocus")]
[Trait("Category", "Integration")]
public sealed class DeleteKeepsSelectionTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private MainWindowView? _window;
	private List<Step> _expectedSteps = null!;
	private bool _transposed;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
	}

	public ValueTask DisposeAsync()
	{
		_window?.Close();
		_viewModel.Dispose();

		return _fixture.DisposeAsync();
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void RepeatedDelete_RemovesTheStepAtTheSamePositionEachTime(bool transposed)
	{
		ShowWithSteps(stepCount: 6, transposed);
		ClickStep(2);

		for (var press = 1; press <= 3; press++)
		{
			PressKey(PhysicalKey.Delete);

			_expectedSteps.RemoveAt(2);
			ShouldSelectWithGridFocus(2, $"Delete press {press} removes the step that took position 3");
		}
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void DeleteOfTheLastStep_SelectsTheNewLastStep(bool transposed)
	{
		ShowWithSteps(stepCount: 4, transposed);
		ClickStep(3);

		PressKey(PhysicalKey.Delete);
		_expectedSteps.RemoveAt(3);
		ShouldSelectWithGridFocus(2, "the delete removed the end of the recipe");

		PressKey(PhysicalKey.Delete);
		_expectedSteps.RemoveAt(2);
		ShouldSelectWithGridFocus(1, "the next Delete removes the new last step");
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void DeleteOfTheOnlyStep_SelectsNothing_AndCtrlNAddsAStep(bool transposed)
	{
		ShowWithSteps(stepCount: 1, transposed);
		ClickStep(0);

		PressKey(PhysicalKey.Delete);

		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(0);
		_viewModel.RecipeGrid.SelectedStepIndices.Should().BeEmpty("an empty recipe has nothing to select");
		((ActiveRecipeGridSurface)_viewModel.RecipeGrid).IsRecipeEmpty.Should().BeTrue("the empty hint shows");

		PressKey(PhysicalKey.N, RawInputModifiers.Control);

		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(1, "Ctrl+N works on the emptied recipe");
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void MultiSelectDelete_SelectsTheOneStepAtTheFirstRemovedPosition(bool transposed)
	{
		ShowWithSteps(stepCount: 6, transposed);
		ClickStep(1);
		ClickStep(3, RawInputModifiers.Control);
		_viewModel.RecipeGrid.SelectedStepIndices.Should().Equal([1, 3], "precondition: Ctrl+click added a step");

		PressKey(PhysicalKey.Delete);

		_expectedSteps.RemoveAt(3);
		_expectedSteps.RemoveAt(1);
		ShouldSelectWithGridFocus(1, "a multi-step delete selects one step");

		PressKey(PhysicalKey.Delete);

		_expectedSteps.RemoveAt(1);
		ShouldSelectWithGridFocus(1, "the next Delete removes that step");
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void RepeatedCut_RemovesTheStepAtTheSamePositionEachTime(bool transposed)
	{
		ShowWithSteps(stepCount: 5, transposed);
		ClickStep(1);

		for (var press = 1; press <= 2; press++)
		{
			PressKey(PhysicalKey.X, RawInputModifiers.Control);

			_expectedSteps.RemoveAt(1);
			ShouldSelectWithGridFocus(1, $"Ctrl+X press {press} removes the step that took position 2");
		}
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void ToolBarDelete_ReturnsFocusToTheGrid_SoTheDeleteKeyRemovesTheNextStep(bool transposed)
	{
		ShowWithSteps(stepCount: 5, transposed);
		ClickStep(1);

		var deleteButton = _window!.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "DeleteButton");
		Click(Center(deleteButton));

		_expectedSteps.RemoveAt(1);
		ShouldSelectWithGridFocus(1, "the toolbar button hands focus back to the grid");

		PressKey(PhysicalKey.Delete);

		_expectedSteps.RemoveAt(1);
		ShouldSelectWithGridFocus(1, "the Delete key after the button removes the next step");
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void UndoAfterDelete_RestoresTheStep_AndKeepsGridFocus(bool transposed)
	{
		ShowWithSteps(stepCount: 4, transposed);
		ClickStep(1);
		var stepsBefore = _expectedSteps.ToList();

		PressKey(PhysicalKey.Delete);
		PressKey(PhysicalKey.Z, RawInputModifiers.Control);

		_fixture.Coordinator.CurrentRecipe.Steps.Should().Equal(stepsBefore, "undo restores the deleted step");
		FocusIsOnTheGrid().Should().BeTrue("undo keeps keyboard focus on the grid");
	}

	private void ShowWithSteps(int stepCount, bool transposed)
	{
		_transposed = transposed;
		_fixture.SeedRecipe(stepCount);
		for (var i = 0; i < stepCount; i++)
		{
			_fixture.Coordinator.UpdateStepProperty(
				i,
				RecipeTestDriver.StepDurationColumn,
				(i + 1).ToString(CultureInfo.InvariantCulture));
		}

		_expectedSteps = [.. _fixture.Coordinator.CurrentRecipe.Steps];
		_viewModel.RecipeGrid.Initialize();

		_window = new MainWindowView { ViewModel = _viewModel };
		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		if (transposed)
		{
			_viewModel.ToggleOrientationCommand.Execute().Subscribe();
			Dispatcher.UIThread.RunJobs();
		}
	}

	private void PressKey(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
	{
		_window!.KeyPressQwerty(key, modifiers);
		Dispatcher.UIThread.RunJobs();
	}

	private void ClickStep(int index, RawInputModifiers modifiers = RawInputModifiers.None)
	{
		Click(Center(DurationCell(index)), modifiers);
	}

	private void Click(Point point, RawInputModifiers modifiers = RawInputModifiers.None)
	{
		_window!.MouseDown(point, MouseButton.Left, modifiers);
		_window!.MouseUp(point, MouseButton.Left, modifiers);
		Dispatcher.UIThread.RunJobs();
	}

	private Point Center(Control control)
	{
		var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), _window!);
		point.Should().NotBeNull();

		return point!.Value;
	}

	private Control DurationCell(int index)
	{
		if (_transposed)
		{
			return StepListBox().ContainerFromIndex(index)!
				.GetVisualDescendants()
				.OfType<Border>()
				.Single(border => border.Classes.Contains("transposed-cell")
					&& border.DataContext is ParameterCellViewModel cell
					&& cell.Descriptor.ParameterKey == RecipeTestDriver.StepDurationColumn);
		}

		var dataGrid = DataGrid();
		var durationColumn = dataGrid.Columns.Single(column =>
			(column.Tag as string) == RecipeTestDriver.StepDurationColumn);
		var row = ((CanonicalRecipeGridSurface)dataGrid.DataContext!).RecipeRows[index];

		return durationColumn.GetCellContent(row)!.FindAncestorOfType<DataGridCell>()!;
	}

	private void ShouldSelectWithGridFocus(int expectedIndex, string because)
	{
		_fixture.Coordinator.CurrentRecipe.Steps.Should().Equal(_expectedSteps, because);
		_viewModel.RecipeGrid.SelectedStepIndices.Should().Equal([expectedIndex], because);
		NativeSelectedIndices().Should().Equal([expectedIndex], because);
		FocusIsOnTheGrid().Should().BeTrue(because);
	}

	private List<int> NativeSelectedIndices()
	{
		if (_transposed)
		{
			return [.. StepListBox().Selection.SelectedIndexes];
		}

		var dataGrid = DataGrid();
		var rows = ((CanonicalRecipeGridSurface)dataGrid.DataContext!).RecipeRows;

		return [.. dataGrid.SelectedItems.OfType<RecipeRowViewModel>().Select(rows.IndexOf).Order()];
	}

	// The transposed panel recycles containers in place, so focus must sit in the selected step's visible container.
	private bool FocusIsOnTheGrid()
	{
		if (_window!.FocusManager?.GetFocusedElement() is not Visual focused)
		{
			return false;
		}

		var target = _transposed
			? StepListBox().ContainerFromIndex(StepListBox().SelectedIndex) ?? StepListBox()
			: DataGrid();

		return target.IsEffectivelyVisible
			&& (ReferenceEquals(focused, target) || target.IsVisualAncestorOf(focused));
	}

	private TransposedStepListBox StepListBox()
	{
		return _window!.GetVisualDescendants().OfType<TransposedStepListBox>().Single();
	}

	private DataGrid DataGrid()
	{
		return _window!.GetVisualDescendants().OfType<DataGrid>().Single();
	}
}
