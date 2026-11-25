using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.RecipeGrid;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "ChangeHighlight")]
[Trait("Category", "Integration")]
public sealed class ClearChangeMarksTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private ActiveRecipeGridSurface _grid = null!;
	private MainWindowView? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
		_grid = (ActiveRecipeGridSurface)_viewModel.RecipeGrid;
		_fixture.SeedRecipe(2);
		_grid.Initialize();
	}

	public ValueTask DisposeAsync()
	{
		_window?.Close();
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	private ICommand Command => _viewModel.RecipeCommands.ClearChangeMarksCommand;

	[AvaloniaFact]
	public void Command_IsDisabled_WithNoMarks()
	{
		Command.CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Command_IsEnabled_AfterAnActionChange()
	{
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		Command.CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void OnePress_ClearsEveryMark_OnBothSurfaces_WithoutAnUndoEntry()
	{
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);
		_fixture.Coordinator.ChangeStepAction(1, RecipeTestDriver.ForLoopActionId);
		var canUndoBefore = _fixture.Coordinator.CanUndo;
		var dirtyBefore = _fixture.Coordinator.IsDirty;
		TransposedRows().Should().OnlyContain(row => row.ChangedColumns.Count > 0, "precondition: the mirror rows carry marks");

		Command.Execute(null);

		AllRows().Should().OnlyContain(row => row.ChangedColumns.Count == 0);
		Command.CanExecute(null).Should().BeFalse();
		_fixture.Coordinator.IsDirty.Should().Be(dirtyBefore);
		_fixture.Coordinator.CanUndo.Should().Be(canUndoBefore);
		_fixture.Coordinator.Undo();
		_fixture.Coordinator.CurrentRecipe.Steps[1].ActionKey.Should().Be(
			RecipeTestDriver.WaitActionId, "the next undo reverts the action change, not the clear");
	}

	[AvaloniaFact]
	public void Command_TurnsDisabled_WhenTheLastMarkGoesByEdit()
	{
		var row = _grid.CanonicalSurface.RecipeRows[0];
		row.MarkChanged([RecipeTestDriver.StepDurationColumn]);
		Command.CanExecute(null).Should().BeTrue("precondition: one mark");

		row.SetPropertyValue(RecipeTestDriver.StepDurationColumn, "15");

		Command.CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Command_TurnsDisabled_WhenTheLastMarkGoesByClickAway()
	{
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);
		var row = _grid.CanonicalSurface.RecipeRows[0];

		foreach (var key in row.ChangedColumns.ToList())
		{
			_grid.CanonicalSurface.ClearChangedByClickAway(row, key);
		}

		Command.CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Command_TurnsDisabled_AtExecutionStart()
	{
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();

		Command.CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Command_TurnsDisabled_OnAFullRebuild()
	{
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		_fixture.Coordinator.NewRecipe();

		Command.CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Command_TurnsDisabled_WhenTheLastMarkedRowIsDeleted()
	{
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		_fixture.Coordinator.RemoveStep(0);

		Command.CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void MenuAndContextMenu_HoldTheItem_BoundToTheCommand()
	{
		_window = new MainWindowView { ViewModel = _viewModel };
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		var menuBar = _window.GetVisualDescendants().OfType<RecipeMenuBar>().Single();
		var editItems = menuBar.GetLogicalDescendants().OfType<MenuItem>()
			.Where(item => ReferenceEquals(item.Command, Command))
			.ToList();
		editItems.Should().ContainSingle();
		editItems[0].InputGesture.Should().BeNull("the item has no shortcut");

		var host = _window.GetVisualDescendants().OfType<RecipeGridHost>().Single();
		var panel = host.GetVisualAncestors().OfType<Panel>().First(candidate => candidate.ContextMenu is not null);
		panel.ContextMenu!.Open(panel);
		Dispatcher.UIThread.RunJobs();
		panel.ContextMenu.Items.OfType<MenuItem>().Should().Contain(item => ReferenceEquals(item.Command, Command));
		panel.ContextMenu.Close();
	}

	private IEnumerable<RecipeRowViewModel> AllRows()
	{
		return _grid.CanonicalSurface.RecipeRows.Concat(TransposedRows());
	}

	private IEnumerable<RecipeRowViewModel> TransposedRows()
	{
		return _grid.TransposedSurface.StepColumns.Select(column => column.Row);
	}
}
