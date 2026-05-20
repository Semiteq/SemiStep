using System.Reactive;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using ReactiveUI;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeFile;

using Xunit;

namespace SemiStep.Tests.UI.RecipeFile;

[Trait("Component", "UI")]
[Trait("Area", "RecipeFile")]
[Trait("Category", "Integration")]
public sealed class RecipeFileViewModelCanExecuteTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private RecipeFileViewModel _recipeFile = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_recipeFile = new RecipeFileViewModel(_fixture.Coordinator, _fixture.MessagePanel);
	}

	public async ValueTask DisposeAsync()
	{
		_recipeFile.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void LoadRecipe_CanExecuteTrue_WhenSyncDisabled()
	{
		((System.Windows.Input.ICommand)_recipeFile.LoadRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void LoadRecipe_CanExecuteFalse_WhenSyncEnabled()
	{
		_fixture.SetSyncEnabled(true);

		((System.Windows.Input.ICommand)_recipeFile.LoadRecipeCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void LoadRecipe_CanExecuteBackToTrue_AfterSyncDisabledAgain()
	{
		_fixture.SetSyncEnabled(true);
		_fixture.SetSyncEnabled(false);

		((System.Windows.Input.ICommand)_recipeFile.LoadRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void NewRecipe_CanExecuteTrue_WhenSyncDisabled()
	{
		((System.Windows.Input.ICommand)_recipeFile.NewRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void NewRecipe_CanExecuteFalse_WhenSyncEnabled()
	{
		_fixture.SetSyncEnabled(true);

		((System.Windows.Input.ICommand)_recipeFile.NewRecipeCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void NewRecipe_CanExecuteBackToTrue_AfterSyncDisabledAgain()
	{
		_fixture.SetSyncEnabled(true);
		_fixture.SetSyncEnabled(false);

		((System.Windows.Input.ICommand)_recipeFile.NewRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void SaveRecipe_CanExecuteRemainsTrue_InBothModes()
	{
		((System.Windows.Input.ICommand)_recipeFile.SaveRecipeCommand).CanExecute(null).Should().BeTrue();

		_fixture.SetSyncEnabled(true);

		((System.Windows.Input.ICommand)_recipeFile.SaveRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void SaveAsRecipe_CanExecuteRemainsTrue_InBothModes()
	{
		((System.Windows.Input.ICommand)_recipeFile.SaveAsRecipeCommand).CanExecute(null).Should().BeTrue();

		_fixture.SetSyncEnabled(true);

		((System.Windows.Input.ICommand)_recipeFile.SaveAsRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void NewRecipe_GatedInvocation_InConnectMode_DoesNotMutateRecipe()
	{
		// End-to-end: simulate the binding-time invocation pattern used by the UI
		// (button click respects CanExecute). In Connect mode the gate refuses
		// invocation, so no mutation occurs.
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(SemiStep.Tests.Core.Helpers.RecipeTestDriver.WaitActionId);
		var stepCountBefore = _fixture.Coordinator.CurrentRecipe.StepCount;
		_fixture.SetSyncEnabled(true);

		var command = (System.Windows.Input.ICommand)_recipeFile.NewRecipeCommand;
		if (command.CanExecute(null))
		{
			command.Execute(null);
		}

		command.CanExecute(null).Should().BeFalse();
		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(stepCountBefore);
	}

	[AvaloniaFact]
	public void LoadRecipe_GatedInvocation_InConnectMode_DoesNotOpenDialog()
	{
		// End-to-end: the dialog handler must not be invoked because CanExecute is
		// false in Connect mode (UI buttons honor CanExecute and never call Execute).
		var interactionInvoked = false;
		_recipeFile.OpenFileInteraction.RegisterHandler((IInteractionContext<Unit, string?> ctx) =>
		{
			interactionInvoked = true;
			ctx.SetOutput(null);
		});
		_fixture.SetSyncEnabled(true);

		var command = (System.Windows.Input.ICommand)_recipeFile.LoadRecipeCommand;
		if (command.CanExecute(null))
		{
			command.Execute(null);
		}

		command.CanExecute(null).Should().BeFalse();
		interactionInvoked.Should().BeFalse();
	}
}
