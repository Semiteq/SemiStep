using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.UI.Helpers;
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
		_viewModel.IsSyncNoLink.Should().Be(expectedNoLink);
		_viewModel.IsSyncLinked.Should().Be(expectedLinked);
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
		_fixture.PlcSyncService.Status = status;

		_viewModel.PlcSyncStatusText.Should().Be(expected);
	}
}
