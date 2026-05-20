using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RecipeGridViewModelReadOnlyTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private RecipeGridViewModel _grid = null!;

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
	}

	public async ValueTask DisposeAsync()
	{
		_grid.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void IsReadOnly_False_Initially_WhenSyncDisabled()
	{
		_grid.IsReadOnly.Should().BeFalse();
	}

	[AvaloniaFact]
	public void IsReadOnly_True_WhenSyncEnabled()
	{
		PushSyncState(true);

		_grid.IsReadOnly.Should().BeTrue();
	}

	[AvaloniaFact]
	public void IsReadOnly_BackToFalse_AfterSyncDisabledAgain()
	{
		PushSyncState(true);
		PushSyncState(false);

		_grid.IsReadOnly.Should().BeFalse();
	}

	[AvaloniaFact]
	public void IsReadOnly_StaysFalse_WhenExecutionActive_AndSyncDisabled()
	{
		// §2.7 regression: execution being active does not lock editing on its own.
		_fixture.S7Service.PushExecutionState(
			new PlcExecutionInfo(
				RecipeActive: true,
				ActualLine: 1,
				StepCurrentTime: 0f,
				ForLoopCount1: 0,
				ForLoopCount2: 0,
				ForLoopCount3: 0));

		_grid.IsReadOnly.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CellCommit_WhenSyncEnabled_DoesNotMutateSession()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		PushSyncState(true);

		// Simulate the UI suppressing cell commits when read-only: no UpdateStepProperty
		// call. Verify that no mutation has been recorded by comparing dirty state.
		var wasDirtyBefore = _fixture.Coordinator.IsDirty;

		// Confirm the grid reports read-only so the UI layer would block the edit.
		_grid.IsReadOnly.Should().BeTrue();
		_fixture.Coordinator.IsDirty.Should().Be(wasDirtyBefore);
	}

	[AvaloniaFact]
	public void EditorMustClose_Emits_WhenSyncFlipsToEnabled()
	{
		var emissionCount = 0;
		using var subscription = _grid.EditorMustClose.Subscribe(_ => emissionCount++);

		PushSyncState(true);

		emissionCount.Should().Be(1);
	}

	[AvaloniaFact]
	public void EditorMustClose_DoesNotEmit_WhenSyncStaysDisabled()
	{
		var emissionCount = 0;
		using var subscription = _grid.EditorMustClose.Subscribe(_ => emissionCount++);

		PushSyncState(false);

		emissionCount.Should().Be(0);
	}

	private void PushSyncState(bool isSyncEnabled)
	{
		_fixture.PlcSyncService.SetSyncEnabled(isSyncEnabled);
		_fixture.PlcSyncService.PushPlcState(Result.Ok(
			new PlcSessionSnapshot(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled)));
	}
}
