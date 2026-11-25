using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI;
using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;
using SemiStep.UI.Plc;

using Xunit;

namespace SemiStep.Tests.UI.Plc;

[Trait("Component", "UI")]
[Trait("Area", "StatusBar")]
[Trait("Category", "Unit")]
public sealed class PlcLinkViewModelTests : IAsyncLifetime
{
	private static readonly PlcLinkAddress _presetAddress = new("192.168.0.150", 102, IsOverride: false);

	private readonly UIFixture _fixture = new();
	private readonly Subject<SyncToggleDirection> _toggle = new();
	private PlcLinkViewModel _viewModel = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = Create(_presetAddress);
	}

	public ValueTask DisposeAsync()
	{
		_viewModel.Dispose();
		_toggle.Dispose();
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void SyncOff_ShowsNoDot()
	{
		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled: false);

		ShouldShow(PlcLinkKind.SyncOff, Resources.LinkSyncOff);
		_viewModel.View.Should().Match<PlcLinkView>(view => !view.IsGreenDot && !view.IsRedDot && !view.IsHollowDot);
	}

	[AvaloniaFact]
	public void EnableInFlight_ShowsConnectingWithAHollowDot()
	{
		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled: false);
		StartToggle(SyncToggleDirection.Enabling);
		Push(PlcConnectionState.Connecting, PlcSyncStatus.Idle, isSyncEnabled: true);

		ShouldShow(PlcLinkKind.Connecting, Resources.LinkConnecting);
		_viewModel.View.IsHollowDot.Should().BeTrue();
	}

	[AvaloniaFact]
	public void DisableInFlight_ShowsDisconnectingWithAHollowDot()
	{
		Push(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: true);
		StartToggle(SyncToggleDirection.Disabling);
		Push(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: false);

		ShouldShow(PlcLinkKind.Disconnecting, Resources.LinkDisconnecting);
		_viewModel.View.IsHollowDot.Should().BeTrue();
	}

	[AvaloniaFact]
	public void DisableInFlight_RanksAboveSyncOff_UntilTheLinkIsDown()
	{
		Push(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: true);
		StartToggle(SyncToggleDirection.Disabling);
		Push(PlcConnectionState.Connected, PlcSyncStatus.Idle, isSyncEnabled: false);
		var whileConnected = _viewModel.View.Kind;

		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled: false);

		new[] { whileConnected, _viewModel.View.Kind }.Should().Equal(PlcLinkKind.Disconnecting, PlcLinkKind.SyncOff);
	}

	[AvaloniaFact]
	public void FailedEnable_NeverShowsDisconnecting()
	{
		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled: false);
		StartToggle(SyncToggleDirection.Enabling);
		Push(PlcConnectionState.Connecting, PlcSyncStatus.Idle, isSyncEnabled: true);
		Push(PlcConnectionState.Connecting, PlcSyncStatus.Idle, isSyncEnabled: false);

		_viewModel.View.Kind.Should().Be(PlcLinkKind.SyncOff);
	}

	[AvaloniaFact]
	public void ReconnectRetries_ShowNoLinkWithTheAge_NotConnecting()
	{
		_fixture.StubS7.LastExchangeTime = _fixture.Scheduler.Now - TimeSpan.FromSeconds(7);
		Push(PlcConnectionState.Connecting, PlcSyncStatus.Synced, isSyncEnabled: true);

		ShouldShow(PlcLinkKind.NoLink, NoLinkText(AgoText(7)));
		_viewModel.View.IsRedDot.Should().BeTrue();
	}

	[AvaloniaFact]
	public void SyncOnWithoutLink_ShowsNoLinkWithARedDot()
	{
		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled: true);

		_viewModel.View.Kind.Should().Be(PlcLinkKind.NoLink);
		_viewModel.View.IsRedDot.Should().BeTrue();
	}

	[AvaloniaFact]
	public void SyncOnWithLink_ShowsConnectedWithAGreenDot()
	{
		Push(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: true);

		ShouldShow(PlcLinkKind.Connected, Resources.LinkConnected);
		_viewModel.View.IsGreenDot.Should().BeTrue();
	}

	[AvaloniaFact]
	public void Failed_SetsTheSyncError()
	{
		Push(PlcConnectionState.Connected, PlcSyncStatus.Failed, isSyncEnabled: true);

		ShouldShow(PlcLinkKind.SyncError, Resources.LinkSyncError);
		_viewModel.View.IsRedDot.Should().BeTrue();
	}

	[AvaloniaFact]
	public void SyncError_LatchesFromTheSnapshot_NotFromTheLiveStatus()
	{
		_fixture.PlcSyncService.SetSyncEnabled(true);
		_fixture.PlcSyncService.Status = PlcSyncStatus.Synced;
		_fixture.PlcSyncService.PushPlcState(
			new PlcSessionSnapshot(PlcConnectionState.Connected, PlcSyncStatus.Failed, IsSyncEnabled: true));
		Dispatcher.UIThread.RunJobs();

		ShouldShow(PlcLinkKind.SyncError, Resources.LinkSyncError);
	}

	[AvaloniaTheory]
	[InlineData(PlcSyncStatus.Syncing)]
	[InlineData(PlcSyncStatus.OutOfSync)]
	public void SyncError_HoldsThrough(PlcSyncStatus next)
	{
		Push(PlcConnectionState.Connected, PlcSyncStatus.Failed, isSyncEnabled: true);
		Push(PlcConnectionState.Connected, next, isSyncEnabled: true);

		_viewModel.View.Kind.Should().Be(PlcLinkKind.SyncError);
	}

	[AvaloniaFact]
	public void SyncError_ShowsAgainAfterTheLinkReturns()
	{
		Push(PlcConnectionState.Connected, PlcSyncStatus.Failed, isSyncEnabled: true);
		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Failed, isSyncEnabled: true);
		Push(PlcConnectionState.Connected, PlcSyncStatus.Failed, isSyncEnabled: true);

		_viewModel.View.Kind.Should().Be(PlcLinkKind.SyncError, "the error shows again after reconnect until Synced");
	}

	[AvaloniaFact]
	public void Synced_ClearsTheSyncError()
	{
		Push(PlcConnectionState.Connected, PlcSyncStatus.Failed, isSyncEnabled: true);
		Push(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: true);

		ShouldShow(PlcLinkKind.Connected, Resources.LinkConnected);
	}

	[AvaloniaFact]
	public void Disable_ClearsTheSyncError()
	{
		Push(PlcConnectionState.Connected, PlcSyncStatus.Failed, isSyncEnabled: true);
		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled: false);
		Push(PlcConnectionState.Connected, PlcSyncStatus.OutOfSync, isSyncEnabled: true);

		_viewModel.View.Kind.Should().Be(PlcLinkKind.Connected);
	}

	[AvaloniaFact]
	public void RecipeActiveAtConnect_KeepsConnected_AndLogsAnInfoEntry()
	{
		Push(PlcConnectionState.Connected, PlcSyncStatus.Idle, isSyncEnabled: true);

		_fixture.PlcSyncService.PushFault(new RecipeActiveError());
		Dispatcher.UIThread.RunJobs();

		_viewModel.View.Kind.Should().Be(PlcLinkKind.Connected);
		_fixture.MessagePanel.Entries.Should().ContainSingle()
			.Which.Severity.Should().Be(MessageSeverity.Info);
	}

	[AvaloniaTheory]
	[InlineData(7, null)]
	[InlineData(45, nameof(Resources.LinkAgeOver30Seconds))]
	[InlineData(120, nameof(Resources.LinkAgeOverMinute))]
	[InlineData(4000, nameof(Resources.LinkAgeOverHour))]
	public void ToolTip_CarriesTheAddressAndTheLastExchangeAge(int ageSeconds, string? bucketKey)
	{
		_fixture.StubS7.LastExchangeTime = _fixture.Scheduler.Now - TimeSpan.FromSeconds(ageSeconds);
		Push(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: true);
		var age = bucketKey is null ? AgoText(ageSeconds) : Resources.ResourceManager.GetString(bucketKey, Resources.Culture)!;

		_viewModel.View.ToolTip.Should()
			.Contain(string.Format(CultureInfo.InvariantCulture, Resources.LinkAddressFormat, "192.168.0.150", 102))
			.And.Contain(string.Format(CultureInfo.InvariantCulture, Resources.LinkLastExchangeFormat, age));
	}

	[AvaloniaFact]
	public void Override_AppendsTheHostToTheBarText()
	{
		using var overridden = Create(new PlcLinkAddress("127.0.0.1", 10102, IsOverride: true));
		Push(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: true);

		overridden.View.Text.Should().Be($"{Resources.LinkConnected}, 127.0.0.1");
		_viewModel.View.Text.Should().Be(Resources.LinkConnected, "no override, the address stays in the tooltip");
	}

	[AvaloniaFact]
	public void Override_IsNeverAppendedInTheNoLinkState()
	{
		using var overridden = Create(new PlcLinkAddress("127.0.0.1", 10102, IsOverride: true));
		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled: true);

		overridden.View.Text.Should().NotContain("127.0.0.1");
	}

	[AvaloniaFact]
	public void NoLink_ShowsTheAgeSinceTheLastSuccessfulRead_AndItGrows()
	{
		_fixture.StubS7.LastExchangeTime = _fixture.Scheduler.Now - TimeSpan.FromSeconds(3);
		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Synced, isSyncEnabled: true);
		var before = _viewModel.View.Text;

		_fixture.Scheduler.AdvanceBy(TimeSpan.FromSeconds(2));
		Dispatcher.UIThread.RunJobs();

		new[] { before, _viewModel.View.Text }.Should().Equal(NoLinkText(AgoText(3)), NoLinkText(AgoText(5)));
	}

	[AvaloniaFact]
	public void NoLink_WithoutAnyExchange_ShowsNever()
	{
		Push(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled: true);

		_viewModel.View.Text.Should().Be(NoLinkText(Resources.LinkNever));
	}

	private PlcLinkViewModel Create(PlcLinkAddress address)
	{
		return new PlcLinkViewModel(
			_fixture.Coordinator,
			address,
			_toggle.AsObservable(),
			_fixture.Scheduler);
	}

	private void Push(PlcConnectionState connection, PlcSyncStatus status, bool isSyncEnabled)
	{
		_fixture.PlcSyncService.SetSyncEnabled(isSyncEnabled);
		_fixture.PlcSyncService.Status = status;
		_fixture.PlcSyncService.PushPlcState(new PlcSessionSnapshot(connection, status, isSyncEnabled));
		Dispatcher.UIThread.RunJobs();
	}

	private void StartToggle(SyncToggleDirection direction)
	{
		_toggle.OnNext(direction);
		Dispatcher.UIThread.RunJobs();
	}

	private void ShouldShow(PlcLinkKind kind, string text)
	{
		_viewModel.View.Kind.Should().Be(kind);
		_viewModel.View.Text.Should().Be(text);
	}

	private static string AgoText(int seconds)
	{
		return string.Format(CultureInfo.InvariantCulture, Resources.LinkAgeSecondsFormat, seconds);
	}

	private static string NoLinkText(string age)
	{
		return string.Format(CultureInfo.InvariantCulture, Resources.LinkNoLinkFormat, age);
	}
}
