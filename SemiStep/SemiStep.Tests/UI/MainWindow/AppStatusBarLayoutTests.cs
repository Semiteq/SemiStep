using System.Reactive.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using AwesomeAssertions;
using AwesomeAssertions.Execution;

using FluentResults;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Shared;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;
using SemiStep.UI.Plc;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "StatusBar")]
[Trait("Category", "Unit")]
public sealed class AppStatusBarLayoutTests : IAsyncLifetime
{
	private static readonly (string Name, double Width)[] _slots =
	[
		("LinkSlot", 270),
		("StateStepSlot", 200),
		("LoopSlot", 190),
		("StepTimerSlot", 134),
		("RecipeTimerSlot", 154),
		("CountsSlot", 80)
	];

	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private StatusBarWindow? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
	}

	public ValueTask DisposeAsync()
	{
		_window?.Dispose();
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Slots_KeepTheirWidthAndPosition_InEveryState()
	{
		_window = new StatusBarWindow(_viewModel);
		var baseline = MeasureSlots();
		using var scope = new AssertionScope();

		foreach (var (name, width) in _slots)
		{
			baseline[name].Width.Should().Be(width, "slot {0} has a fixed width", name);
		}

		baseline["CountsSlot"].Right.Should().Be(StatusBarWindow.WindowWidth - 12, "the counts slot sits at the right end");

		foreach (var (state, apply) in States())
		{
			apply();
			_window.Settle();

			MeasureSlots().Should().Equal(baseline, "no slot moves or resizes in state '{0}'", state);
		}
	}

	[AvaloniaFact]
	public void CountsSlotClick_TogglesTheLog()
	{
		_window = new StatusBarWindow(_viewModel);
		_fixture.MessagePanel.RefreshReasons([new Error("e"), new Warning("w")]);
		_window.Settle();
		var wasVisible = _fixture.MessagePanel.IsVisible;

		_window.Click("CountsSlot");
		var afterFirst = _fixture.MessagePanel.IsVisible;
		_window.Click("CountsSlot");

		new[] { afterFirst, _fixture.MessagePanel.IsVisible }.Should().Equal(!wasVisible, wasVisible);
	}

	[AvaloniaFact]
	public void LinkSlotClick_OpensTheLog_AndShowsTheLinkText()
	{
		_window = new StatusBarWindow(_viewModel);
		_fixture.MessagePanel.RefreshReasons([new Error("e")]);
		_fixture.MessagePanel.IsVisible = false;
		_window.Settle();

		_window.Click("LinkSlot");

		_fixture.MessagePanel.ShowPanel.Should().BeTrue();
		_window.Slot<TextBlock>("LinkText").Text.Should().Be(_viewModel.PlcLink.View.Text);
	}

	[AvaloniaFact]
	public async Task RealSyncToggle_ShowsConnecting_WhileTheEnableIsInFlight()
	{
		var gate = new ReadGate();
		_fixture.StubS7.ProtocolVersionReadGate = gate;

		var enabling = _viewModel.ToggleSyncCommand.Execute().ToTask();
		await gate.Entered.WaitAsync(TestContext.Current.CancellationToken);
		PushLink(PlcConnectionState.Connecting, PlcSyncStatus.Idle, isSyncEnabled: true);
		Dispatcher.UIThread.RunJobs();
		var whileEnabling = _viewModel.PlcLink.View.Kind;
		gate.Release();
		await enabling;
		PushLink(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: true);
		Dispatcher.UIThread.RunJobs();

		whileEnabling.Should().Be(PlcLinkKind.Connecting);
		_viewModel.PlcLink.View.Kind.Should().Be(PlcLinkKind.Connected, "the finished toggle no longer holds the slot");
		_viewModel.PlcLink.View.Text.Should().Be(Resources.LinkConnected);
	}

	[AvaloniaFact]
	public void LockGlyph_ShowsOnlyWhileRunning_AndTheStateTextStaysPut()
	{
		new RecipeTestDriver(_fixture.Session).NewRecipe().AddWait().AddWait();
		_window = new StatusBarWindow(_viewModel);
		var lockGlyph = _window.Slot<PathIcon>("LockGlyph");
		var idleVisible = lockGlyph.IsVisible;
		var idleTextX = _window.BoundsInBar("StateStepText").X;

		_fixture.SetRecipeActive(true);
		_window.Settle();

		new[] { idleVisible, lockGlyph.IsVisible }.Should().Equal(false, true);
		_window.BoundsInBar("StateStepText").X.Should().Be(idleTextX, "the state text keeps its place when a run starts");
	}

	[AvaloniaFact]
	public void ErrorDot_StaysPut_WhenTheCountGrowsAndWarningsAppear()
	{
		_window = new StatusBarWindow(_viewModel);
		ShowMessages(errors: 9, warnings: 0);
		var nineErrorsX = _window.BoundsInBar("ErrorDot").X;

		ShowMessages(errors: 10, warnings: 0);
		var tenErrorsX = _window.BoundsInBar("ErrorDot").X;
		ShowMessages(errors: 10, warnings: 1);

		new[] { tenErrorsX, _window.BoundsInBar("ErrorDot").X }.Should().Equal(nineErrorsX, nineErrorsX);
	}

	[AvaloniaFact]
	public void LinkText_UsesTheBarFont_LikeTheOtherSlotTexts()
	{
		_window = new StatusBarWindow(_viewModel);
		var linkText = _window.Slot<TextBlock>("LinkText");
		var stateText = _window.Slot<TextBlock>("StateStepText");

		(linkText.FontSize, linkText.FontWeight).Should().Be((stateText.FontSize, stateText.FontWeight));
	}

	private void ShowMessages(int errors, int warnings)
	{
		var reasons = Enumerable.Range(0, errors).Select(i => (IReason)new Error($"e{i}"))
			.Concat(Enumerable.Range(0, warnings).Select(i => (IReason)new Warning($"w{i}")));
		_fixture.MessagePanel.RefreshReasons(reasons);
		_window!.Settle();
	}

	private IEnumerable<(string Name, Action Apply)> States()
	{
		var enableGate = new ReadGate();

		yield return ("errors and warnings", () => _fixture.MessagePanel.RefreshReasons([new Error("e"), new Warning("w")]));
		yield return ("no messages", () => _fixture.MessagePanel.RefreshReasons([]));
		yield return ("sync off", () => PushLink(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled: false));
		yield return ("connecting", () => StartEnable(enableGate));
		yield return ("connected", () => FinishEnable(enableGate));
		yield return ("sync error", () => PushLink(PlcConnectionState.Connected, PlcSyncStatus.Failed, isSyncEnabled: true));
		yield return ("no link", () => PushLink(PlcConnectionState.Disconnected, PlcSyncStatus.Synced, isSyncEnabled: true));
		yield return ("connected again", () => PushLink(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: true));
		yield return ("running", StartNestedRun);
		yield return ("running in a 3-level loop", () => PushExecution(line: 3, depth1: 2, depth2: 1, depth3: 1));
		yield return ("idle", () => _fixture.SetRecipeActive(false));
	}

	private void StartNestedRun()
	{
		new RecipeTestDriver(_fixture.Session).NewRecipe()
			.AddFor(5).AddFor(3).AddFor(2).AddWait().AddEndFor().AddEndFor().AddEndFor();
		PushExecution(line: 0, depth1: 0, depth2: 0, depth3: 0);
	}

	private void PushExecution(int line, int depth1, int depth2, int depth3)
	{
		_fixture.StubS7.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: line,
			StepCurrentTime: 0f,
			ForLoopCount1: depth1,
			ForLoopCount2: depth2,
			ForLoopCount3: depth3));
	}

	private void StartEnable(ReadGate gate)
	{
		_fixture.StubS7.ProtocolVersionReadGate = gate;
		_viewModel.ToggleSyncCommand.Execute().Subscribe();
		PushLink(PlcConnectionState.Connecting, PlcSyncStatus.Idle, isSyncEnabled: true);
	}

	private void FinishEnable(ReadGate gate)
	{
		gate.Release();
		PushLink(PlcConnectionState.Connected, PlcSyncStatus.Synced, isSyncEnabled: true);
	}

	private void PushLink(PlcConnectionState connection, PlcSyncStatus status, bool isSyncEnabled)
	{
		_fixture.PlcSyncService.SetSyncEnabled(isSyncEnabled);
		_fixture.PlcSyncService.PushPlcState(new PlcSessionSnapshot(connection, status, isSyncEnabled));
	}

	private Dictionary<string, Rect> MeasureSlots()
	{
		return _slots.ToDictionary(slot => slot.Name, slot => _window!.BoundsInBar(slot.Name));
	}
}
