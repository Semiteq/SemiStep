using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Plc.State;
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
		PushSyncState(true);

		((System.Windows.Input.ICommand)_recipeFile.LoadRecipeCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void LoadRecipe_CanExecuteBackToTrue_AfterSyncDisabledAgain()
	{
		PushSyncState(true);
		PushSyncState(false);

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
		PushSyncState(true);

		((System.Windows.Input.ICommand)_recipeFile.NewRecipeCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void NewRecipe_CanExecuteBackToTrue_AfterSyncDisabledAgain()
	{
		PushSyncState(true);
		PushSyncState(false);

		((System.Windows.Input.ICommand)_recipeFile.NewRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void SaveRecipe_CanExecuteRemainsTrue_InBothModes()
	{
		((System.Windows.Input.ICommand)_recipeFile.SaveRecipeCommand).CanExecute(null).Should().BeTrue();

		PushSyncState(true);

		((System.Windows.Input.ICommand)_recipeFile.SaveRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void SaveAsRecipe_CanExecuteRemainsTrue_InBothModes()
	{
		((System.Windows.Input.ICommand)_recipeFile.SaveAsRecipeCommand).CanExecute(null).Should().BeTrue();

		PushSyncState(true);

		((System.Windows.Input.ICommand)_recipeFile.SaveAsRecipeCommand).CanExecute(null).Should().BeTrue();
	}

	private void PushSyncState(bool isSyncEnabled)
	{
		_fixture.PlcSyncService.SetSyncEnabled(isSyncEnabled);
		_fixture.PlcSyncService.PushPlcState(Result.Ok(
			new PlcSessionSnapshot(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled)));
	}
}
