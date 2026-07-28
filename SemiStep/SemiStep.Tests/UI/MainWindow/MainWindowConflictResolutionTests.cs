using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Recipes;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "PlcConflict")]
[Trait("Category", "Integration")]
public sealed class MainWindowConflictResolutionTests : IAsyncLifetime
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
	public async Task HandleConflict_KeepLocal_ResolvesKeepingLocalRecipe()
	{
		_viewModel.ResolveConflictInteraction.RegisterHandler(context => context.SetOutput(true));
		var callsBefore = _fixture.PlcSyncService.NotifyRecipeChangedCallCount;

		await _viewModel.HandleConflictAsync(CurrentRecipe, CurrentRecipe);

		_fixture.PlcSyncService.NotifyRecipeChangedCallCount.Should().Be(
			callsBefore + 1,
			"keeping local must resolve the conflict by pushing the local recipe back to the PLC");
		_fixture.MessagePanel.Entries.Should().NotContain(
			entry => entry.Severity == MessageSeverity.Error,
			"a successful keep-local resolution must not report an error");
	}

	[AvaloniaFact]
	public async Task HandleConflict_LoadFromPlc_ResolvesLoadingPlcRecipe()
	{
		_viewModel.ResolveConflictInteraction.RegisterHandler(context => context.SetOutput(false));
		var callsBefore = _fixture.PlcSyncService.NotifyRecipeChangedCallCount;

		await _viewModel.HandleConflictAsync(CurrentRecipe, CurrentRecipe);

		// HandleConflictAsync is driven directly, so no pending PLC recipe was seeded by a real
		// conflict; the load branch therefore fails on the missing pending recipe. That failure is
		// the faithful observable proof that keepLocal=false reached RecipeCoordinator.ResolveConflict,
		// distinct from the keep-local branch which pushes via NotifyRecipeChanged instead.
		_fixture.PlcSyncService.NotifyRecipeChangedCallCount.Should().Be(
			callsBefore,
			"loading from the PLC must not push the local recipe back");
		_fixture.MessagePanel.Entries.Should().Contain(
			entry => entry.Severity == MessageSeverity.Error && entry.Message.Contains("pending PLC recipe"),
			"the load-from-PLC branch must route through ResolveConflict(false)");
	}

	[AvaloniaFact]
	public async Task HandleConflict_Cancel_DoesNothing()
	{
		_viewModel.ResolveConflictInteraction.RegisterHandler(context => context.SetOutput(null));
		var callsBefore = _fixture.PlcSyncService.NotifyRecipeChangedCallCount;

		await _viewModel.HandleConflictAsync(CurrentRecipe, CurrentRecipe);

		_fixture.PlcSyncService.NotifyRecipeChangedCallCount.Should().Be(
			callsBefore,
			"cancelling the dialog must not resolve the conflict either way");
		_fixture.MessagePanel.Entries.Should().NotContain(
			entry => entry.Severity == MessageSeverity.Error,
			"cancelling must not report any error");
	}

	[AvaloniaFact]
	public async Task HandleConflict_HandlerThrows_ContainsFaultAndReports()
	{
		_viewModel.ResolveConflictInteraction.RegisterHandler(
			_ => throw new InvalidOperationException("dialog boom"));

		var act = async () => await _viewModel.HandleConflictAsync(CurrentRecipe, CurrentRecipe);

		await act.Should().NotThrowAsync("a dialog fault must be contained on the fire-and-forget path");
		_fixture.MessagePanel.Entries.Should().Contain(
			entry => entry.Severity == MessageSeverity.Error && entry.Message == "Failed to show PLC conflict dialog");
	}

	[AvaloniaFact]
	public async Task HandleConflict_NoHandlerRegistered_ReportsInsteadOfVanishing()
	{
		await _viewModel.HandleConflictAsync(CurrentRecipe, CurrentRecipe);

		_fixture.MessagePanel.Entries.Should().Contain(
			entry => entry.Severity == MessageSeverity.Error && entry.Message == "Failed to show PLC conflict dialog",
			"an unhandled interaction must convert today's silent drop into a report");
	}

	private Recipe CurrentRecipe => _fixture.Coordinator.CurrentRecipe;
}
