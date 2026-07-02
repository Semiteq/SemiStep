using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.Localization;
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
	[InlineData(false, false, true, false, false)]
	[InlineData(false, true, true, false, false)]
	[InlineData(true, false, false, true, false)]
	[InlineData(true, true, false, false, true)]
	public void SyncStateBooleans_ReflectEnabledAndConnected(
		bool isSyncEnabled,
		bool isConnected,
		bool expectedLocalMode,
		bool expectedNoLink,
		bool expectedLinked)
	{
		_fixture.PlcSyncService.SetSyncEnabled(isSyncEnabled);
		_fixture.S7Service.IsConnected = isConnected;

		_viewModel.IsSyncLocalMode.Should().Be(expectedLocalMode);
		_viewModel.IsSyncConnecting.Should().BeFalse();
		_viewModel.IsSyncNoLink.Should().Be(expectedNoLink);
		_viewModel.IsSyncLinked.Should().Be(expectedLinked);

		CountTrueStates().Should().Be(1, "the four sync states are mutually exclusive");
		_viewModel.IsSyncOnIdle.Should().Be(isSyncEnabled, "no attempt is in flight, so ON idles whenever sync is enabled");
	}

	[AvaloniaFact]
	public async Task SyncStateBooleans_DuringConnecting_OnlyConnectingIsTrueAndTextGatesHide()
	{
		_fixture.PlcSyncService.SetSyncEnabled(true);
		_fixture.S7Service.IsConnected = false;
		_fixture.PlcSyncService.PushPlcState(
			new PlcSessionSnapshot(PlcConnectionState.Connecting, PlcSyncStatus.Idle, IsSyncEnabled: true));

		await TestHelpers.WaitUntilAsync(
			() => _fixture.Coordinator.IsConnecting,
			cancellationToken: TestContext.Current.CancellationToken);

		_viewModel.IsSyncConnecting.Should().BeTrue();
		CountTrueStates().Should().Be(1, "the four sync states are mutually exclusive");

		_viewModel.IsSyncOnIdle.Should().BeFalse("the \"Sync ON\" label hides while connecting");
		_viewModel.IsSyncLocalMode.Should().BeFalse("the \"Sync OFF\" label hides while connecting");
	}

	[AvaloniaFact]
	public async Task SyncStateBooleans_WhenConnectingCoincidesWithConnected_OnlyConnectingIsTrue()
	{
		_fixture.PlcSyncService.SetSyncEnabled(true);
		_fixture.S7Service.IsConnected = true;
		_fixture.PlcSyncService.PushPlcState(
			new PlcSessionSnapshot(PlcConnectionState.Connecting, PlcSyncStatus.Idle, IsSyncEnabled: true));

		await TestHelpers.WaitUntilAsync(
			() => _fixture.Coordinator.IsConnecting,
			cancellationToken: TestContext.Current.CancellationToken);

		_viewModel.IsSyncConnecting.Should().BeTrue("connecting wins over connected");
		_viewModel.IsSyncLinked.Should().BeFalse("linked is excluded structurally while connecting");
		CountTrueStates().Should().Be(1, "the four sync states stay mutually exclusive even if connecting coincides with connected");
	}

	[AvaloniaFact]
	public async Task RaiseConnectionStateProperties_OnConnectingSnapshot_RaisesConnectingAndOnIdle()
	{
		var raised = new List<string>();
		_viewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName ?? string.Empty);

		_fixture.PlcSyncService.SetSyncEnabled(true);
		_fixture.PlcSyncService.PushPlcState(
			new PlcSessionSnapshot(PlcConnectionState.Connecting, PlcSyncStatus.Idle, IsSyncEnabled: true));

		await TestHelpers.WaitUntilAsync(
			() => _fixture.Coordinator.IsConnecting,
			cancellationToken: TestContext.Current.CancellationToken);

		raised.Should().Contain(nameof(MainWindowViewModel.IsSyncConnecting));
		raised.Should().Contain(nameof(MainWindowViewModel.IsSyncOnIdle));
	}

	[AvaloniaFact]
	public void PlcSyncStatusText_ForOutOfSync_ReturnsEmpty()
	{
		_fixture.PlcSyncService.Status = PlcSyncStatus.OutOfSync;

		_viewModel.PlcSyncStatusText.Should().BeEmpty();
	}

	[AvaloniaTheory]
	[InlineData(PlcSyncStatus.Idle, "Idle")]
	[InlineData(PlcSyncStatus.Syncing, "Syncing...")]
	[InlineData(PlcSyncStatus.Synced, "Synced")]
	[InlineData(PlcSyncStatus.Failed, "Failed")]
	public void PlcSyncStatusText_ForPipelineStatuses_ReturnsText(PlcSyncStatus status, string expected)
	{
		using (ResourcesCultureScope.Use("en"))
		{
			_fixture.PlcSyncService.Status = status;

			_viewModel.PlcSyncStatusText.Should().Be(expected);
		}
	}

	[AvaloniaFact]
	public void WindowTitle_NewRecipe_UnderRussianCulture_UsesRussianLabelAndKeepsBrand()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			_viewModel.WindowTitle.Should().Be("SemiStep - Новый рецепт");
		}
	}

	private int CountTrueStates()
	{
		var states = new[]
		{
			_viewModel.IsSyncLocalMode,
			_viewModel.IsSyncConnecting,
			_viewModel.IsSyncLinked,
			_viewModel.IsSyncNoLink
		};

		return states.Count(state => state);
	}
}
