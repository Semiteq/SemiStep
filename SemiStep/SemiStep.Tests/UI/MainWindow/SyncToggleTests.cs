using System.Reactive.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Integration")]
public sealed class SyncToggleTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private MainWindowView? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
	}

	public ValueTask DisposeAsync()
	{
		_window?.Close();
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	[AvaloniaTheory]
	[InlineData(false, PlcConnectionState.Disconnected)]
	[InlineData(true, PlcConnectionState.Disconnected)]
	[InlineData(true, PlcConnectionState.Connecting)]
	[InlineData(true, PlcConnectionState.Connected)]
	public void ToggleAndMenuItem_AreChecked_ExactlyWhileSyncIsEnabled(bool isSyncEnabled, PlcConnectionState link)
	{
		var window = ShowMainWindow();

		_fixture.PlcSyncService.SetSyncEnabled(isSyncEnabled);
		_fixture.PlcSyncService.PushPlcState(new PlcSessionSnapshot(link, PlcSyncStatus.Idle, isSyncEnabled));
		Dispatcher.UIThread.RunJobs();

		ToggleOf(window).IsChecked.Should().Be(isSyncEnabled);
		MenuItemOf(window).IsChecked.Should().Be(isSyncEnabled);
	}

	[AvaloniaFact]
	public async Task Toggle_ClickedWhenEnableFails_ReturnsToOff()
	{
		var window = ShowMainWindow();
		_fixture.StubS7.ConnectShouldFail = true;
		var toggle = ToggleOf(window);

		Click(window, TrackOf(toggle));
		await TestHelpers.WaitUntilAsync(
			() => _fixture.MessagePanel.Entries.Any(entry => entry.Severity == MessageSeverity.Error),
			TimeSpan.FromSeconds(10),
			TestHelpers.DefaultPollInterval,
			TestContext.Current.CancellationToken);
		Dispatcher.UIThread.RunJobs();

		_viewModel.IsSyncEnabled.Should().BeFalse("precondition: the enable failed");
		toggle.IsChecked.Should().BeFalse("the toggle shows the view model's state, not its own click");
		MenuItemOf(window).IsChecked.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task Toggle_ClickedWhenOwnershipIsRefused_ReturnsToOff()
	{
		var window = ShowMainWindow();
		_fixture.SyncOwnership.ShouldRefuse = true;
		var toggle = ToggleOf(window);

		Click(window, TrackOf(toggle));
		await WaitForErrorAsync();

		_fixture.SyncOwnership.TryAcquireCallCount.Should().Be(1, "precondition: the enable was refused");
		_viewModel.IsSyncEnabled.Should().BeFalse("precondition: sync stayed off");
		toggle.IsChecked.Should().BeFalse("the toggle shows the view model's state, not its own click");
		MenuItemOf(window).IsChecked.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task MenuItem_ClickedWhenOwnershipIsRefused_StaysOff()
	{
		var window = ShowMainWindow();
		_fixture.SyncOwnership.ShouldRefuse = true;
		var fileMenu = window.GetVisualDescendants().OfType<RecipeMenuBar>().Single()
			.GetVisualDescendants().OfType<MenuItem>().First();
		fileMenu.IsSubMenuOpen = true;
		Dispatcher.UIThread.RunJobs();
		var item = MenuItemOf(window);

		Click(window, item);
		await WaitForErrorAsync();

		_fixture.SyncOwnership.TryAcquireCallCount.Should().Be(1, "precondition: the enable was refused");
		_viewModel.IsSyncEnabled.Should().BeFalse("precondition: sync stayed off");
		item.IsChecked.Should().BeFalse();
		ToggleOf(window).IsChecked.Should().BeFalse();
	}

	private async Task WaitForErrorAsync()
	{
		await TestHelpers.WaitUntilAsync(
			() => _fixture.MessagePanel.Entries.Any(entry => entry.Severity == MessageSeverity.Error),
			TimeSpan.FromSeconds(10),
			TestHelpers.DefaultPollInterval,
			TestContext.Current.CancellationToken);
		Dispatcher.UIThread.RunJobs();
	}

	[AvaloniaFact]
	public void ToggleAndMenuItem_StayEnabled_DuringABackgroundReconnect()
	{
		var window = ShowMainWindow();

		_fixture.PlcSyncService.SetSyncEnabled(true);
		_fixture.PlcSyncService.PushPlcState(new PlcSessionSnapshot(PlcConnectionState.Connecting, PlcSyncStatus.Idle, true));
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.IsConnecting.Should().BeTrue("precondition: the link is reconnecting");
		ToggleOf(window).IsEffectivelyEnabled.Should().BeTrue();
		MenuItemOf(window).Command!.CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task ToggleAndMenuItem_AreDisabled_WhileADisableWaitsForAWrite()
	{
		var window = ShowMainWindow();
		await _viewModel.ToggleSyncCommand.Execute();
		Dispatcher.UIThread.RunJobs();
		_viewModel.IsSyncEnabled.Should().BeTrue("precondition: sync is on");

		var pendingWrite = new TaskCompletionSource();
		_fixture.PlcSyncService.PendingSync = pendingWrite.Task;
		var toggle = ToggleOf(window);
		toggle.Command!.Execute(null);
		Dispatcher.UIThread.RunJobs();

		toggle.IsEffectivelyEnabled.Should().BeFalse("a switch is in progress");
		MenuItemOf(window).Command!.CanExecute(null).Should().BeFalse("the menu item shares the command");

		pendingWrite.SetResult();
		await TestHelpers.WaitUntilAsync(
			() => toggle.IsEffectivelyEnabled,
			cancellationToken: TestContext.Current.CancellationToken);
		Dispatcher.UIThread.RunJobs();

		toggle.IsChecked.Should().BeFalse();
		MenuItemOf(window).Command!.CanExecute(null).Should().BeTrue();
	}

	[AvaloniaFact]
	public void Toggle_CheckedTrack_IsNeutralGrey()
	{
		var window = ShowMainWindow();
		var toggle = ToggleOf(window);
		var track = TrackOf(toggle);
		track.Transitions = null;

		_fixture.PlcSyncService.SetSyncEnabled(true);
		_fixture.PlcSyncService.PushPlcState(new PlcSessionSnapshot(PlcConnectionState.Connected, PlcSyncStatus.Idle, true));
		Dispatcher.UIThread.RunJobs();

		toggle.IsChecked.Should().BeTrue("precondition: sync is on");
		track.Background.Should().BeAssignableTo<ISolidColorBrush>().Which.Color
			.Should().Be(Color.Parse("#6C707E"), "the checked track is neutral grey, not accent or green");
	}

	[AvaloniaFact]
	public async Task Toggle_DisabledWhileChecked_ShowsGreyTrack()
	{
		var window = ShowMainWindow();
		await _viewModel.ToggleSyncCommand.Execute();
		Dispatcher.UIThread.RunJobs();
		var toggle = ToggleOf(window);
		var track = TrackOf(toggle);
		track.Transitions = null;

		var pendingWrite = new TaskCompletionSource();
		_fixture.PlcSyncService.PendingSync = pendingWrite.Task;
		toggle.Command!.Execute(null);
		Dispatcher.UIThread.RunJobs();

		toggle.IsChecked.Should().BeTrue("precondition: sync is still on while the disable waits");
		toggle.IsEffectivelyEnabled.Should().BeFalse("precondition: a switch is in progress");
		track.Background.Should().BeAssignableTo<ISolidColorBrush>().Which.Color
			.Should().Be(Color.Parse("#A8ADBD"), "a disabled checked track is grey, never green");

		pendingWrite.SetResult();
		await TestHelpers.WaitUntilAsync(
			() => toggle.IsEffectivelyEnabled,
			cancellationToken: TestContext.Current.CancellationToken);
	}

	[AvaloniaTheory]
	[InlineData(SyncTogglePart.Caption)]
	[InlineData(SyncTogglePart.Track)]
	public async Task Toggle_ClickedOnEitherPart_TurnsSyncOn(SyncTogglePart part)
	{
		var window = ShowMainWindow();
		var toggle = ToggleOf(window);
		Control target = part == SyncTogglePart.Caption ? CaptionOf(toggle) : TrackOf(toggle);

		Click(window, target);
		await TestHelpers.WaitUntilAsync(
			() => _viewModel.IsSyncEnabled && toggle.IsEffectivelyEnabled,
			TimeSpan.FromSeconds(10),
			TestHelpers.DefaultPollInterval,
			TestContext.Current.CancellationToken);
		Dispatcher.UIThread.RunJobs();

		toggle.IsChecked.Should().BeTrue();
		MenuItemOf(window).IsChecked.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task Toggle_PressOnCaptionMoveTenPixelsAndRelease_RunsTheCommandOnce()
	{
		var window = ShowMainWindow();
		var toggle = ToggleOf(window);
		var runs = 0;
		using var subscription = _viewModel.ToggleSyncCommand.Subscribe(_ => runs++);
		var caption = CaptionOf(toggle);
		var start = caption.TranslatePoint(new Point(caption.Bounds.Width / 2, caption.Bounds.Height / 2), window)!.Value;
		var end = new Point(start.X + 10, start.Y);

		window.MouseDown(start, MouseButton.Left);
		window.MouseMove(end, RawInputModifiers.LeftMouseButton);
		window.MouseUp(end, MouseButton.Left);
		await WaitForSwitchAsync(toggle, () => runs > 0);

		runs.Should().Be(1);
		toggle.IsChecked.Should().Be(_viewModel.IsSyncEnabled);
	}

	[AvaloniaTheory]
	[InlineData(Key.Space, PhysicalKey.Space, " ")]
	[InlineData(Key.Enter, PhysicalKey.Enter, "\r")]
	public async Task Toggle_KeyWithFocus_RunsTheCommand(Key key, PhysicalKey physicalKey, string keySymbol)
	{
		var window = ShowMainWindow();
		var toggle = ToggleOf(window);
		var runs = 0;
		using var subscription = _viewModel.ToggleSyncCommand.Subscribe(_ => runs++);
		toggle.Focus().Should().BeTrue("precondition: the toggle takes keyboard focus");

		window.KeyPress(key, RawInputModifiers.None, physicalKey, keySymbol);
		window.KeyRelease(key, RawInputModifiers.None, physicalKey, keySymbol);
		await WaitForSwitchAsync(toggle, () => runs > 0);

		runs.Should().Be(1);
		toggle.IsChecked.Should().Be(_viewModel.IsSyncEnabled);
	}

	[AvaloniaFact]
	public void Toggle_TrackKeepsItsSize_AndTheKnobMovesBetweenOffAndOn()
	{
		var window = ShowMainWindow();
		var toggle = ToggleOf(window);
		var track = TrackOf(toggle);
		var offSize = track.Bounds.Size;
		var offKnobLeft = KnobLeftIn(toggle, track);

		_fixture.PlcSyncService.SetSyncEnabled(true);
		_fixture.PlcSyncService.PushPlcState(new PlcSessionSnapshot(PlcConnectionState.Connected, PlcSyncStatus.Idle, true));
		Dispatcher.UIThread.RunJobs();

		toggle.IsChecked.Should().BeTrue("precondition: sync is on");
		offSize.Should().Be(new Size(40, 24), "Semi's default track: an 18 px knob with a 3 px margin");
		track.Bounds.Size.Should().Be(offSize);
		KnobLeftIn(toggle, track).Should().BeGreaterThan(offKnobLeft, "the knob moves right when the switch is on");
	}

	private static double KnobLeftIn(ToggleSwitch toggle, Border track)
	{
		var knob = toggle.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "SwitchKnobIndicator");

		return knob.TranslatePoint(new Point(0, 0), track)!.Value.X;
	}

	private static async Task WaitForSwitchAsync(ToggleSwitch toggle, Func<bool> commandRan)
	{
		Dispatcher.UIThread.RunJobs();
		await TestHelpers.WaitUntilAsync(
			() => commandRan() && toggle.IsEffectivelyEnabled,
			TimeSpan.FromSeconds(10),
			TestHelpers.DefaultPollInterval,
			TestContext.Current.CancellationToken);
		Dispatcher.UIThread.RunJobs();
	}

	[AvaloniaFact]
	public void Toggle_CaptionSitsLeftOfTrack_OnTheButtonLine()
	{
		var window = ShowMainWindow();
		var toggle = ToggleOf(window);
		var caption = CaptionOf(toggle);
		var track = TrackOf(toggle);
		var toolBar = window.GetVisualDescendants().OfType<RecipeToolBar>().Single();
		var openButton = toolBar.FindControl<Button>("OpenButton")!;
		var buttonRow = (Control)toolBar.FindControl<StackPanel>("Buttons")!.Parent!;

		var captionBounds = BoundsIn(caption, window);
		var trackBounds = BoundsIn(track, window);
		var buttonBounds = BoundsIn(openButton, window);

		trackBounds.Left.Should().BeApproximately(captionBounds.Right + 8, 0.5, "the switch sits 8 px right of the caption");
		trackBounds.Center.Y.Should().BeApproximately(captionBounds.Center.Y, 0.5, "caption and switch share one line");
		captionBounds.Center.Y.Should().BeApproximately(buttonBounds.Center.Y, 0.5, "the caption sits on the button line");
		toggle.DesiredSize.Height.Should().BeLessThanOrEqualTo(openButton.Bounds.Height, "the switch fits the button row");
		buttonRow.Bounds.Height.Should().Be(openButton.Bounds.Height, "the switch does not make the toolbar taller");
		toggle.Bounds.Height.Should().Be(openButton.Bounds.Height, "the click target spans the button row");
		var buttonCaption = openButton.GetVisualDescendants().OfType<TextBlock>().First();
		caption.FontSize.Should().Be(buttonCaption.FontSize, "the caption matches the toolbar button captions");
		caption.FontWeight.Should().Be(buttonCaption.FontWeight, "the caption matches the toolbar button captions");
	}

	[AvaloniaFact]
	public void StatusBar_HasNoSyncControl()
	{
		var window = ShowMainWindow();

		var statusBar = window.GetVisualDescendants().OfType<AppStatusBar>().Single();

		statusBar.GetVisualDescendants().OfType<Button>()
			.Should().NotContain(button => ReferenceEquals(button.Command, _viewModel.ToggleSyncCommand));
	}

	private static void Click(MainWindowView window, Control control)
	{
		var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
		window.MouseDown(center, MouseButton.Left);
		window.MouseUp(center, MouseButton.Left);
		Dispatcher.UIThread.RunJobs();
	}

	private static Rect BoundsIn(Control control, MainWindowView window)
	{
		var topLeft = control.TranslatePoint(new Point(0, 0), window)!.Value;

		return new Rect(topLeft, control.Bounds.Size);
	}

	private static ContentPresenter CaptionOf(ToggleSwitch toggle)
	{
		return toggle.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "PART_ContentPresenter");
	}

	private static Border TrackOf(ToggleSwitch toggle)
	{
		return toggle.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "SwitchBackgroundBorder");
	}

	private static ToggleSwitch ToggleOf(MainWindowView window)
	{
		return window.GetVisualDescendants().OfType<ToggleSwitch>().Single(toggle => toggle.Name == "SyncToggle");
	}

	private MenuItem MenuItemOf(MainWindowView window)
	{
		var menuBar = window.GetVisualDescendants().OfType<RecipeMenuBar>().Single();
		var item = menuBar.FindControl<MenuItem>("SyncMenuItem")!;
		item.Command.Should().BeSameAs(_viewModel.ToggleSyncCommand);

		return item;
	}

	private MainWindowView ShowMainWindow()
	{
		_window = new MainWindowView { ViewModel = _viewModel };
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return _window;
	}
}

public enum SyncTogglePart
{
	Caption,
	Track,
}
