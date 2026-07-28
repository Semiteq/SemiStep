using System.Windows.Input;

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
	private CanonicalRecipeGridSurface _surface = null!;
	private RecipeCommandsViewModel _commands = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_surface = _fixture.CreateCanonicalSurface();
		_surface.Initialize();
		_commands = new RecipeCommandsViewModel(
			_fixture.Coordinator,
			_surface,
			_fixture.MessagePanel,
			NullLogger<RecipeCommandsViewModel>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		_commands.Dispose();
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void AddStep_CanExecuteTrue_WhenSyncDisabled()
	{
		((ICommand)_commands.AddStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void AddStep_CanExecuteFalse_WhenRecipeExecuting()
	{
		_fixture.SetRecipeActive(true);

		((ICommand)_commands.AddStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void AddStep_CanExecuteTrue_WhenSyncEnabledButNotExecuting()
	{
		_fixture.SetSyncEnabled(true);

		((ICommand)_commands.AddStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void AddStep_CanExecuteBackToTrue_AfterExecutionStops()
	{
		_fixture.SetRecipeActive(true);
		_fixture.SetRecipeActive(false);

		((ICommand)_commands.AddStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void DeleteStep_CanExecuteTrue_WhenSyncDisabledAndRowSelected()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.UpdateSelection(new[] { 0 });

		((ICommand)_commands.DeleteStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void DeleteStep_CanExecuteFalse_WhenRecipeExecuting_EvenWithSelection()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.UpdateSelection(new[] { 0 });
		_fixture.SetRecipeActive(true);

		((ICommand)_commands.DeleteStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void DeleteStep_CanExecuteFalse_WhenSyncDisabledButNoSelection()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.UpdateSelection(Array.Empty<int>());

		((ICommand)_commands.DeleteStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void DeleteStep_CanExecuteTrue_WhenSelectionExistsBeforeConstruction()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.UpdateSelection(new[] { 0 });

		using var commands = new RecipeCommandsViewModel(
			_fixture.Coordinator,
			_surface,
			_fixture.MessagePanel,
			NullLogger<RecipeCommandsViewModel>.Instance);

		((ICommand)commands.DeleteStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Undo_CanExecuteTrue_WhenSyncDisabledAndUndoAvailable()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		((ICommand)_commands.UndoCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Undo_CanExecuteFalse_WhenRecipeExecuting_EvenWithUndoAvailable()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.SetRecipeActive(true);

		((ICommand)_commands.UndoCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Redo_CanExecuteTrue_WhenSyncDisabledAndRedoAvailable()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.Undo();

		((ICommand)_commands.RedoCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Redo_CanExecuteFalse_WhenRecipeExecuting_EvenWithRedoAvailable()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.Undo();
		_fixture.SetRecipeActive(true);

		((ICommand)_commands.RedoCommand).CanExecute(null).Should().BeFalse();
	}
}
