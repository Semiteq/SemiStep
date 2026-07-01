using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "Coordinator")]
[Trait("Category", "Integration")]
public sealed class RecipeCoordinatorConnectionStateTests : IAsyncLifetime
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
	public async Task ConnectionStateTransitions_TrackIsConnectingAndIsConnected()
	{
		_fixture.S7Service.IsConnected = false;
		PushPlcState(PlcConnectionState.Connecting);
		await WaitForConnectingAsync(true);

		_fixture.Coordinator.IsConnecting.Should().BeTrue();
		_fixture.Coordinator.IsConnected.Should().BeFalse();

		_fixture.S7Service.IsConnected = true;
		PushPlcState(PlcConnectionState.Connected);
		await WaitForConnectingAsync(false);

		_fixture.Coordinator.IsConnecting.Should().BeFalse();
		_fixture.Coordinator.IsConnected.Should().BeTrue();

		_fixture.S7Service.IsConnected = false;
		PushPlcState(PlcConnectionState.Connecting);
		await WaitForConnectingAsync(true);

		PushPlcState(PlcConnectionState.Disconnected);
		await WaitForConnectingAsync(false);

		_fixture.Coordinator.IsConnecting.Should().BeFalse();
		_fixture.Coordinator.IsConnected.Should().BeFalse();
	}

	// Catches the cache-before-emit ordering only because RxSchedulers.MainThreadScheduler runs
	// downstream inline on the UI thread, so this subscriber executes synchronously inside OnNext.
	[AvaloniaFact]
	public async Task PlcStateChangedSubscriber_ObservesUpdatedIsConnecting()
	{
		var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var subscription = _fixture.Coordinator.PlcStateChanged
			.Subscribe(_ => observed.TrySetResult(_fixture.Coordinator.IsConnecting));

		PushPlcState(PlcConnectionState.Connecting);

		var observedIsConnecting = await observed.Task.WaitAsync(
			TimeSpan.FromSeconds(2),
			TestContext.Current.CancellationToken);

		observedIsConnecting.Should().BeTrue(
			"the cache is assigned before the emit, so a PlcStateChanged subscriber reads the updated IsConnecting");
	}

	private void PushPlcState(PlcConnectionState state)
	{
		_fixture.PlcSyncService.PushPlcState(
			new PlcSessionSnapshot(state, PlcSyncStatus.Idle, _fixture.PlcSyncService.IsSyncEnabled));
	}

	private Task WaitForConnectingAsync(bool expected)
	{
		return TestHelpers.WaitUntilAsync(
			() => _fixture.Coordinator.IsConnecting == expected,
			cancellationToken: TestContext.Current.CancellationToken);
	}
}
