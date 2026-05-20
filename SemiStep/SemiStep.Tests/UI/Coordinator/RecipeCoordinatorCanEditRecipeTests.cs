using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.UI.Helpers;

using Xunit;

namespace SemiStep.Tests.UI.Coordinator;

[Trait("Component", "UI")]
[Trait("Area", "Coordinator")]
[Trait("Category", "Integration")]
public sealed class RecipeCoordinatorCanEditRecipeTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();

	public ValueTask InitializeAsync()
	{
		return _fixture.InitializeAsync();
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void CanEditRecipe_EmitsTrueOnSubscribe_WhenSyncDisabled()
	{
		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			values.Should().ContainSingle().Which.Should().BeTrue();
		}
		finally
		{
			subscription.Dispose();
		}
	}

	[AvaloniaFact]
	public void CanEditRecipe_FlipsToFalse_WhenSyncEnabled()
	{
		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			PushSyncState(true);

			values.Should().Equal(true, false);
		}
		finally
		{
			subscription.Dispose();
		}
	}

	[AvaloniaFact]
	public void CanEditRecipe_FlipsBackToTrue_WhenSyncDisabledAfterEnabled()
	{
		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			PushSyncState(true);
			PushSyncState(false);

			values.Should().Equal(true, false, true);
		}
		finally
		{
			subscription.Dispose();
		}
	}

	[AvaloniaFact]
	public void CanEditRecipe_HandlesFailureRollback_EnableThenDisable()
	{
		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			// Simulates PlcLifecycleManager.EnableSync setting sync true, then rolling
			// back to false on connection failure (PlcLifecycleManager.cs:110).
			PushSyncState(true);
			PushSyncState(false);

			values.Should().Equal(true, false, true);
		}
		finally
		{
			subscription.Dispose();
		}
	}

	[AvaloniaFact]
	public void CanEditRecipe_LateSubscriber_ReceivesCurrentValue()
	{
		PushSyncState(true);

		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			values.Should().ContainSingle().Which.Should().BeFalse();
		}
		finally
		{
			subscription.Dispose();
		}
	}

	private void PushSyncState(bool isSyncEnabled)
	{
		_fixture.PlcSyncService.SetSyncEnabled(isSyncEnabled);
		_fixture.PlcSyncService.PushPlcState(Result.Ok(
			new PlcSessionSnapshot(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled)));
	}

	private static List<bool> CollectValues(IObservable<bool> source, out IDisposable subscription)
	{
		var values = new List<bool>();
		subscription = source.Subscribe(values.Add);
		return values;
	}
}
