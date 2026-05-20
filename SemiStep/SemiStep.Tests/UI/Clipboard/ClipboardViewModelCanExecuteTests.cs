using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Clipboard;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.Clipboard;

[Trait("Component", "UI")]
[Trait("Area", "Clipboard")]
[Trait("Category", "Integration")]
public sealed class ClipboardViewModelCanExecuteTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private RecipeGridViewModel _grid = null!;
	private ClipboardViewModel _clipboard = null!;

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

		var clipboardSerializer = new ClipboardSerializer(_fixture.RecipeMetadataRegistry);
		var importedRecipeValidator = new ImportedRecipeValidator(_fixture.RecipeMetadataRegistry);

		_clipboard = new ClipboardViewModel(
			_fixture.Coordinator,
			_grid,
			clipboardSerializer,
			importedRecipeValidator,
			_fixture.MessagePanel);
	}

	public async ValueTask DisposeAsync()
	{
		_clipboard.Dispose();
		_grid.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Copy_CanExecuteTrue_WhenSyncDisabledAndRowSelected()
	{
		AppendStepAndSelect();

		((System.Windows.Input.ICommand)_clipboard.CopyStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Copy_CanExecuteRemainsTrue_WhenSyncEnabled()
	{
		AppendStepAndSelect();
		PushSyncState(true);

		((System.Windows.Input.ICommand)_clipboard.CopyStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Cut_CanExecuteTrue_WhenSyncDisabledAndRowSelected()
	{
		AppendStepAndSelect();

		((System.Windows.Input.ICommand)_clipboard.CutStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Cut_CanExecuteFalse_WhenSyncEnabled_EvenWithSelection()
	{
		AppendStepAndSelect();
		PushSyncState(true);

		((System.Windows.Input.ICommand)_clipboard.CutStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Cut_CanExecuteFalse_WhenSyncDisabledButNoSelection()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_grid.SelectedRowIndices = Array.Empty<int>();

		((System.Windows.Input.ICommand)_clipboard.CutStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Cut_CanExecuteBackToTrue_AfterSyncDisabledAgain()
	{
		AppendStepAndSelect();
		PushSyncState(true);
		PushSyncState(false);

		((System.Windows.Input.ICommand)_clipboard.CutStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteTrue_WhenSyncDisabled()
	{
		((System.Windows.Input.ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteFalse_WhenSyncEnabled()
	{
		PushSyncState(true);

		((System.Windows.Input.ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteBackToTrue_AfterSyncDisabledAgain()
	{
		PushSyncState(true);
		PushSyncState(false);

		((System.Windows.Input.ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeTrue();
	}

	private void AppendStepAndSelect()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_grid.SelectedRowIndices = new[] { 0 };
	}

	private void PushSyncState(bool isSyncEnabled)
	{
		_fixture.PlcSyncService.SetSyncEnabled(isSyncEnabled);
		_fixture.PlcSyncService.PushPlcState(Result.Ok(
			new PlcSessionSnapshot(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled)));
	}
}
