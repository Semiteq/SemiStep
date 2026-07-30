using System.Windows.Input;

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
	private CanonicalRecipeGridSurface _surface = null!;
	private ClipboardViewModel _clipboard = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_surface = _fixture.CreateCanonicalSurface();
		_surface.Initialize();

		var clipboardSerializer = new ClipboardSerializer(
			_fixture.RecipeMetadataRegistry,
			NullLogger<ClipboardSerializer>.Instance);
		var importedRecipeValidator = new ImportedRecipeValidator(_fixture.RecipeMetadataRegistry);

		_clipboard = new ClipboardViewModel(
			_fixture.Coordinator,
			_surface,
			clipboardSerializer,
			importedRecipeValidator,
			_fixture.MessagePanel,
			NullLogger<ClipboardViewModel>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		_clipboard.Dispose();
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Copy_CanExecuteTrue_WhenSyncDisabledAndRowSelected()
	{
		AppendStepAndSelect();

		((ICommand)_clipboard.CopyStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Copy_CanExecuteRemainsTrue_WhenSyncEnabled()
	{
		AppendStepAndSelect();
		_fixture.SetSyncEnabled(true);

		((ICommand)_clipboard.CopyStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Cut_CanExecuteTrue_WhenSyncDisabledAndRowSelected()
	{
		AppendStepAndSelect();

		((ICommand)_clipboard.CutStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Cut_CanExecuteFalse_WhenRecipeExecuting_EvenWithSelection()
	{
		AppendStepAndSelect();
		_fixture.SetRecipeActive(true);

		((ICommand)_clipboard.CutStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Cut_CanExecuteFalse_WhenSyncDisabledButNoSelection()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.UpdateSelection(Array.Empty<int>());

		((ICommand)_clipboard.CutStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Cut_CanExecuteBackToTrue_AfterExecutionStops()
	{
		AppendStepAndSelect();
		_fixture.SetRecipeActive(true);
		_fixture.SetRecipeActive(false);

		((ICommand)_clipboard.CutStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Cut_CanExecuteTrue_WhenSelectionExistsBeforeConstruction()
	{
		AppendStepAndSelect();

		using var clipboard = new ClipboardViewModel(
			_fixture.Coordinator,
			_surface,
			new ClipboardSerializer(_fixture.RecipeMetadataRegistry, NullLogger<ClipboardSerializer>.Instance),
			new ImportedRecipeValidator(_fixture.RecipeMetadataRegistry),
			_fixture.MessagePanel,
			NullLogger<ClipboardViewModel>.Instance);

		((ICommand)clipboard.CutStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteTrue_WhenSyncDisabled()
	{
		// Paste's canExecute is driven solely by CanEditRecipe; no clipboard is required
		// for the CanExecute gate.
		((ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteFalse_WhenRecipeExecuting()
	{
		_fixture.SetRecipeActive(true);

		((ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeFalse();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteTrue_WhenSyncEnabledButNotExecuting()
	{
		_fixture.SetSyncEnabled(true);

		((ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Paste_CanExecuteBackToTrue_AfterExecutionStops()
	{
		_fixture.SetRecipeActive(true);
		_fixture.SetRecipeActive(false);

		((ICommand)_clipboard.PasteStepCommand).CanExecute(null).Should().BeTrue();
	}

	private void AppendStepAndSelect()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_surface.UpdateSelection(new[] { 0 });
	}
}
