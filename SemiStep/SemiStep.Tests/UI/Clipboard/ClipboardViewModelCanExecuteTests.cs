using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

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
		_fixture.SetSyncEnabled(true);

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
		_fixture.SetSyncEnabled(true);

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
		_fixture.SetSyncEnabled(true);
		_fixture.SetSyncEnabled(false);

		((System.Windows.Input.ICommand)_clipboard.CutStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteTrue_WhenSyncDisabled()
	{
		// Paste's canExecute is driven solely by CanEditRecipe; no clipboard is required
		// for the CanExecute gate.
		((System.Windows.Input.ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteFalse_WhenSyncEnabled()
	{
		_fixture.SetSyncEnabled(true);

		((System.Windows.Input.ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteBackToTrue_AfterSyncDisabledAgain()
	{
		_fixture.SetSyncEnabled(true);
		_fixture.SetSyncEnabled(false);

		((System.Windows.Input.ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Cut_GatedInvocation_InConnectMode_DoesNotRemoveStep()
	{
		// UI buttons honor CanExecute and never call Execute when gated. Simulate that
		// pattern and assert recipe state is untouched in Connect mode.
		AppendStepAndSelect();
		var stepCountBefore = _fixture.Coordinator.CurrentRecipe.StepCount;
		_fixture.SetSyncEnabled(true);

		var command = (System.Windows.Input.ICommand)_clipboard.CutStepCommand;
		if (command.CanExecute(null))
		{
			command.Execute(null);
		}

		command.CanExecute(null).Should().BeFalse();
		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(stepCountBefore);
	}

	[AvaloniaFact]
	public void Paste_GatedInvocation_InConnectMode_DoesNotInsertSteps()
	{
		_fixture.Coordinator.NewRecipe();
		var stepCountBefore = _fixture.Coordinator.CurrentRecipe.StepCount;
		_fixture.SetSyncEnabled(true);

		var command = (System.Windows.Input.ICommand)_clipboard.PasteStepCommand;
		if (command.CanExecute(null))
		{
			command.Execute(null);
		}

		command.CanExecute(null).Should().BeFalse();
		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(stepCountBefore);
	}

	private void AppendStepAndSelect()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_grid.SelectedRowIndices = new[] { 0 };
	}
}
