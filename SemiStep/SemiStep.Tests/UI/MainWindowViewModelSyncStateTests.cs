using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Unit")]
public sealed class MainWindowViewModelSyncStateTests : IAsyncLifetime
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

	[AvaloniaTheory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public void IsSyncEnabled_FollowsTheSyncService_WhateverTheLink(bool isSyncEnabled, bool isConnected)
	{
		_fixture.PlcSyncService.SetSyncEnabled(isSyncEnabled);
		_fixture.S7Service.IsConnected = isConnected;

		_viewModel.IsSyncEnabled.Should().Be(isSyncEnabled);
	}

	[AvaloniaFact]
	public async Task IsSyncEnabled_StaysOn_WhileConnecting()
	{
		_fixture.PlcSyncService.SetSyncEnabled(true);
		_fixture.S7Service.IsConnected = false;
		_fixture.PlcSyncService.PushPlcState(
			new PlcSessionSnapshot(PlcConnectionState.Connecting, PlcSyncStatus.Idle, IsSyncEnabled: true));

		await TestHelpers.WaitUntilAsync(
			() => _fixture.Coordinator.IsConnecting,
			cancellationToken: TestContext.Current.CancellationToken);

		_viewModel.IsSyncEnabled.Should().BeTrue("connecting is still sync on");
	}

	[AvaloniaFact]
	public async Task RaiseConnectionStateProperties_OnConnectingSnapshot_RaisesIsSyncEnabled()
	{
		var raised = new List<string>();
		_viewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName ?? string.Empty);

		_fixture.PlcSyncService.SetSyncEnabled(true);
		_fixture.PlcSyncService.PushPlcState(
			new PlcSessionSnapshot(PlcConnectionState.Connecting, PlcSyncStatus.Idle, IsSyncEnabled: true));

		await TestHelpers.WaitUntilAsync(
			() => _fixture.Coordinator.IsConnecting,
			cancellationToken: TestContext.Current.CancellationToken);

		raised.Should().Contain(nameof(MainWindowViewModel.IsSyncEnabled));
	}

	[AvaloniaFact]
	public void PlcSyncStatusText_ForOutOfSync_ReturnsEmpty()
	{
		_fixture.PlcSyncService.Status = PlcSyncStatus.OutOfSync;

		_viewModel.PlcSyncStatusText.Should().BeEmpty();
	}

	[AvaloniaTheory]
	[InlineData(PlcSyncStatus.Idle, nameof(Resources.StatusIdle))]
	[InlineData(PlcSyncStatus.Syncing, nameof(Resources.StatusSyncing))]
	[InlineData(PlcSyncStatus.Synced, nameof(Resources.StatusSynced))]
	[InlineData(PlcSyncStatus.Failed, nameof(Resources.StatusFailed))]
	public void PlcSyncStatusText_ForStatus_IsItsOwnResource(PlcSyncStatus status, string resourceKey)
	{
		_fixture.PlcSyncService.Status = status;

		_viewModel.PlcSyncStatusText.Should().Be(Resources.ResourceManager.GetString(resourceKey, Resources.Culture));
	}
}
