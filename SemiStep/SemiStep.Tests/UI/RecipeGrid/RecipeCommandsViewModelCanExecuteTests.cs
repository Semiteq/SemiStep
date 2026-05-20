using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RecipeCommandsViewModelCanExecuteTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private RecipeGridViewModel _grid = null!;
	private RecipeCommandsViewModel _commands = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_grid = new RecipeGridViewModel(
			_fixture.Coordinator,
			_fixture.RecipeMetadataRegistry,
			_fixture.MessagePanel,
			NullLogger<RecipeGridViewModel>.Instance);
		_fixture.Coordinator.Mutated += _grid.OnMutation;
		_grid.Initialize();
		_commands = new RecipeCommandsViewModel(_fixture.Coordinator, _grid);
	}

	public async ValueTask DisposeAsync()
	{
		_commands.Dispose();
		_grid.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void AddStep_CanExecuteTrue_WhenSyncDisabled()
	{
		((System.Windows.Input.ICommand)_commands.AddStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void AddStep_CanExecuteFalse_WhenSyncEnabled()
	{
		_fixture.SetSyncEnabled(true);

		((System.Windows.Input.ICommand)_commands.AddStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void AddStep_CanExecuteBackToTrue_AfterSyncDisabledAgain()
	{
		_fixture.SetSyncEnabled(true);
		_fixture.SetSyncEnabled(false);

		((System.Windows.Input.ICommand)_commands.AddStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void DeleteStep_CanExecuteTrue_WhenSyncDisabledAndRowSelected()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_grid.SelectedRowIndices = new[] { 0 };

		((System.Windows.Input.ICommand)_commands.DeleteStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void DeleteStep_CanExecuteFalse_WhenSyncEnabled_EvenWithSelection()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_grid.SelectedRowIndices = new[] { 0 };
		_fixture.SetSyncEnabled(true);

		((System.Windows.Input.ICommand)_commands.DeleteStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void DeleteStep_CanExecuteFalse_WhenSyncDisabledButNoSelection()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_grid.SelectedRowIndices = Array.Empty<int>();

		((System.Windows.Input.ICommand)_commands.DeleteStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Undo_CanExecuteTrue_WhenSyncDisabledAndUndoAvailable()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		((System.Windows.Input.ICommand)_commands.UndoCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Undo_CanExecuteFalse_WhenSyncEnabled_EvenWithUndoAvailable()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.SetSyncEnabled(true);

		((System.Windows.Input.ICommand)_commands.UndoCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Redo_CanExecuteTrue_WhenSyncDisabledAndRedoAvailable()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.Undo();

		((System.Windows.Input.ICommand)_commands.RedoCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Redo_CanExecuteFalse_WhenSyncEnabled_EvenWithRedoAvailable()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.Undo();
		_fixture.SetSyncEnabled(true);

		((System.Windows.Input.ICommand)_commands.RedoCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void AddStep_GatedInvocation_InConnectMode_DoesNotInsertStep()
	{
		// UI buttons honor CanExecute and never call Execute when gated. Simulate that
		// pattern and assert no mutation reaches the session.
		_fixture.Coordinator.NewRecipe();
		var stepCountBefore = _fixture.Coordinator.CurrentRecipe.StepCount;
		_fixture.SetSyncEnabled(true);

		var command = (System.Windows.Input.ICommand)_commands.AddStepCommand;
		if (command.CanExecute(null))
		{
			command.Execute(null);
		}

		command.CanExecute(null).Should().BeFalse();
		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(stepCountBefore);
	}

	[AvaloniaFact]
	public void DeleteStep_GatedInvocation_InConnectMode_DoesNotRemoveStep()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_grid.SelectedRowIndices = new[] { 0 };
		var stepCountBefore = _fixture.Coordinator.CurrentRecipe.StepCount;
		_fixture.SetSyncEnabled(true);

		var command = (System.Windows.Input.ICommand)_commands.DeleteStepCommand;
		if (command.CanExecute(null))
		{
			command.Execute(null);
		}

		command.CanExecute(null).Should().BeFalse();
		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(stepCountBefore);
	}
}
