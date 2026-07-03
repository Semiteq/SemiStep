using System.Windows.Input;

using Avalonia.Headless.XUnit;

using FluentAssertions;

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
		((ICommand)_recipeFile.LoadRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void LoadRecipe_CanExecuteFalse_WhenRecipeExecuting()
	{
		_fixture.SetRecipeActive(true);

		((ICommand)_recipeFile.LoadRecipeCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void LoadRecipe_CanExecuteTrue_WhenSyncEnabledButNotExecuting()
	{
		_fixture.SetSyncEnabled(true);

		((ICommand)_recipeFile.LoadRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void LoadRecipe_CanExecuteBackToTrue_AfterExecutionStops()
	{
		_fixture.SetRecipeActive(true);
		_fixture.SetRecipeActive(false);

		((ICommand)_recipeFile.LoadRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void NewRecipe_CanExecuteTrue_WhenSyncDisabled()
	{
		((ICommand)_recipeFile.NewRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void NewRecipe_CanExecuteFalse_WhenRecipeExecuting()
	{
		_fixture.SetRecipeActive(true);

		((ICommand)_recipeFile.NewRecipeCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void NewRecipe_CanExecuteBackToTrue_AfterExecutionStops()
	{
		_fixture.SetRecipeActive(true);
		_fixture.SetRecipeActive(false);

		((ICommand)_recipeFile.NewRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void SaveRecipe_CanExecuteRemainsTrue_InBothModes()
	{
		((ICommand)_recipeFile.SaveRecipeCommand).CanExecute(null).Should().BeTrue();

		_fixture.SetSyncEnabled(true);

		((ICommand)_recipeFile.SaveRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void SaveAsRecipe_CanExecuteRemainsTrue_InBothModes()
	{
		((ICommand)_recipeFile.SaveAsRecipeCommand).CanExecute(null).Should().BeTrue();

		_fixture.SetSyncEnabled(true);

		((ICommand)_recipeFile.SaveAsRecipeCommand).CanExecute(null).Should().BeTrue();
	}
}
