using System;
using System.Reactive.Linq;
using System.Windows.Input;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using ReactiveUI;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.Reactive;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Integration")]
public sealed class MainWindowHotkeyTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
	}

	public ValueTask DisposeAsync()
	{
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Hotkey_WhenCommandThrows_DoesNotRethrowAndReportsOnce()
	{
		var failure = new InvalidOperationException("boom");
		var grid = (ActiveRecipeGridSurface)_viewModel.RecipeGrid;

		// The flip raises Orientation; a throwing observer on that change faults the command body,
		// driving the exception into ToggleOrientationCommand.ThrownExceptions.
		using var poison = grid.WhenAnyValue(x => x.Orientation)
			.Skip(1)
			.Subscribe(_ => throw failure);

		var invoke = () => _viewModel.ToggleOrientationCommand.ExecuteIfPossible();

		invoke.Should().NotThrow("ExecuteIfPossible swallows the caller-thread rethrow that would crash the app");

		_fixture.MessagePanel.Entries.Should().ContainSingle(
			entry => entry.Severity == MessageSeverity.Error && entry.Message == $"{Resources.OrientationToggleFailed}: boom");
	}

	[AvaloniaFact]
	public void DeleteHotkey_WhenCanEditFalse_DoesNotMutateRecipe()
	{
		_fixture.SeedRecipe(2);
		_viewModel.RecipeGrid.Initialize();
		_viewModel.RecipeGrid.UpdateSelection(new[] { 0 });

		var deleteCommand = _viewModel.RecipeCommands.DeleteStepCommand;
		((ICommand)deleteCommand).CanExecute(null).Should().BeTrue("precondition: editable with a step selected");

		_fixture.SetRecipeActive(true);
		((ICommand)deleteCommand).CanExecute(null).Should().BeFalse("a running recipe disables editing");

		deleteCommand.ExecuteIfPossible();

		_fixture.Coordinator.CurrentRecipe.Steps.Should()
			.HaveCount(2, "the gated hotkey must stay inert while canEdit is false");
	}

	[AvaloniaFact]
	public void DeleteHotkey_WhenCanEditTrue_DeletesSelectedStep()
	{
		_fixture.SeedRecipe(2);
		_viewModel.RecipeGrid.Initialize();
		_viewModel.RecipeGrid.UpdateSelection(new[] { 0 });

		_viewModel.RecipeCommands.DeleteStepCommand.ExecuteIfPossible();

		_fixture.Coordinator.CurrentRecipe.Steps.Should()
			.HaveCount(1, "ExecuteIfPossible must run the command when canExecute is true");
	}
}
