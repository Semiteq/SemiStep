using Avalonia.Headless.XUnit;

using FluentAssertions;

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
		_fixture.SetSyncEnabled(true);

		_grid.IsReadOnly.Should().BeTrue();
	}

	[AvaloniaFact]
	public void IsReadOnly_BackToFalse_AfterSyncDisabledAgain()
	{
		_fixture.SetSyncEnabled(true);
		_fixture.SetSyncEnabled(false);

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
	public void CellValueChanged_WhenReadOnly_DoesNotMutateSession()
	{
		// Defense in depth: even if the UI commits a cell edit while the grid is
		// read-only, the row -> coordinator bridge must short-circuit and not apply
		// the property update.
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.SetSyncEnabled(true);
		_grid.IsReadOnly.Should().BeTrue();

		var row = _grid.RecipeRows[0];
		var originalRecipe = _fixture.Coordinator.CurrentRecipe;
		var wasDirtyBefore = _fixture.Coordinator.IsDirty;

		row.SetPropertyValue("time", "99");

		_fixture.Coordinator.IsDirty.Should().Be(wasDirtyBefore);
		_fixture.Coordinator.CurrentRecipe.Should().BeSameAs(originalRecipe);
	}

	[AvaloniaFact]
	public void EditorMustClose_Emits_WhenSyncFlipsToEnabled()
	{
		var emissionCount = 0;
		using var subscription = _grid.EditorMustClose.Subscribe(_ => emissionCount++);

		_fixture.SetSyncEnabled(true);

		emissionCount.Should().Be(1);
	}

	[AvaloniaFact]
	public void EditorMustClose_DoesNotEmit_WhenSyncStaysDisabled()
	{
		var emissionCount = 0;
		using var subscription = _grid.EditorMustClose.Subscribe(_ => emissionCount++);

		_fixture.SetSyncEnabled(false);

		emissionCount.Should().Be(0);
	}

	[AvaloniaFact]
	public void EditorMustClose_LateSubscriber_DoesNotReceiveInitialReadOnlyState()
	{
		_fixture.SetSyncEnabled(true);

		var emissionCount = 0;
		using var subscription = _grid.EditorMustClose.Subscribe(_ => emissionCount++);

		emissionCount.Should().Be(0);
	}
}
