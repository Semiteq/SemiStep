using System.Globalization;
using System.Reactive.Concurrency;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.MainWindow;
using SemiStep.UI.Localization;
using SemiStep.UI.Plc;

using Xunit;

namespace SemiStep.Tests.UI.Plc;

[Trait("Component", "UI")]
[Trait("Area", "StatusBar")]
[Trait("Category", "Unit")]
public sealed class ExecutionSlotTests : IAsyncLifetime
{
	private const char FigureSpace = ' ';

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
	public void Idle_ShowsNotRunning()
	{
		Driver().AddWait().AddWait();
		using var monitor = CreateMonitor();

		monitor.StateStepText.Should().Be(Resources.ExecutionIdle);
		monitor.LoopText.Should().BeEmpty();
		monitor.ExecutionToolTip.Should().BeNull();
	}

	[AvaloniaFact]
	public void Running_ShowsTheStepOfTheTotal_WithTheLockToolTip()
	{
		NestedLoopRecipe();
		using var monitor = CreateMonitor();

		Push(Running(line: 5));

		monitor.StateStepText.Should().Be(RunningText("6", 8));
		monitor.ExecutionToolTip.Should().Be(Resources.ExecutionLockToolTip);
	}

	[AvaloniaFact]
	public void StepNumber_IsPaddedWithFigureSpaces_ToTheDigitsOfTheTotal()
	{
		var driver = Driver();
		for (var i = 0; i < 12; i++)
		{
			driver.AddWait();
		}

		using var monitor = CreateMonitor();

		Push(Running(line: 8));

		monitor.StateStepText.Should().Be(RunningText($"{FigureSpace}9", 12));
	}

	[AvaloniaFact]
	public void StepPastTheLocalRecipe_ShowsThePlcStepWithoutATotal()
	{
		Driver().AddWait().AddWait().AddWait();
		using var monitor = CreateMonitor();

		Push(Running(line: 2));
		var lastLocalStep = monitor.StateStepText;
		Push(Running(line: 5));

		new[] { lastLocalStep, monitor.StateStepText }.Should().Equal(
			RunningText("3", 3),
			Format(Resources.ExecutionRunningStepOnlyFormat, 6));
	}

	[AvaloniaFact]
	public void LoopText_IsInnermostFirst_WithTotalsFromTheRecipe_AndOnlyCountedLevels()
	{
		NestedLoopRecipe();
		using var monitor = CreateMonitor();

		Push(Running(line: 2, depth1: 2, depth2: 1));
		var bothLevels = monitor.LoopText;
		Push(Running(line: 2, depth1: 2, depth2: 0));

		new[] { bothLevels, monitor.LoopText }.Should().Equal(
			$"{Format(Resources.LoopPassFirstFormat, 1, 3)} › {Format(Resources.LoopPassNextFormat, 2, 5)}",
			Format(Resources.LoopPassFirstFormat, 2, 5));
	}

	[AvaloniaFact]
	public void ToolTip_HasTheLockTextFirst_ThenEveryLoopLevel()
	{
		NestedLoopRecipe();
		using var monitor = CreateMonitor();

		Push(Running(line: 2, depth1: 2, depth2: 1));

		monitor.ExecutionToolTip.Should().Be(string.Join(
			Environment.NewLine,
			Resources.ExecutionLockToolTip,
			Format(Resources.LoopLevelFirstFormat, 1, 1, 3),
			Format(Resources.LoopLevelNextFormat, 2, 2, 5)));
	}

	[AvaloniaFact]
	public void LoopText_TrimsAtTheEnd()
	{
		var viewModel = _fixture.CreateMainWindowViewModel();
		using var window = new StatusBarWindow(viewModel);

		window.Slot<TextBlock>("LoopText").TextTrimming.Should().Be(TextTrimming.CharacterEllipsis);

		viewModel.Dispose();
	}

	[AvaloniaFact]
	public void WindowTitle_GetsTheRunningSuffix_WhileTheRecipeRuns()
	{
		Driver().AddWait();
		var viewModel = _fixture.CreateMainWindowViewModel();

		Push(Running(line: 0));
		var whileRunning = viewModel.WindowTitle;
		Push(Running(line: 0) with { RecipeActive = false });

		whileRunning.Should().EndWith(Resources.WindowTitleRunningSuffix);
		viewModel.WindowTitle.Should().NotEndWith(Resources.WindowTitleRunningSuffix);

		viewModel.Dispose();
	}

	[AvaloniaFact]
	public void FirstTick_BeforeAnyStep_ShowsRunningWithoutAStep()
	{
		Driver().AddWait().AddWait();
		using var monitor = CreateMonitor();

		Push(Running(line: -1));

		monitor.StateStepText.Should().Be(Resources.ExecutionRunning);
	}

	[AvaloniaFact]
	public void FinishHold_OnePastTheLastStep_ShowsTheLastStepOfTheTotal()
	{
		Driver().AddWait().AddWait().AddWait();
		using var monitor = CreateMonitor();

		Push(Running(line: 3));

		monitor.StateStepText.Should().Be(RunningText("3", 3));
	}

	[AvaloniaTheory]
	[InlineData(PlcConnectionState.Disconnected, false)]
	[InlineData(PlcConnectionState.Connecting, true)]
	[InlineData(PlcConnectionState.Disconnected, true)]
	public void NoLinkMidRun_ShowsNoData_WithoutLockOrTimers(PlcConnectionState link, bool isSyncEnabled)
	{
		NestedLoopRecipe();
		var scheduler = new HistoricalScheduler();
		using var monitor = CreateMonitor(scheduler);
		Push(Running(line: 2, depth1: 2, depth2: 1));

		PushLink(link, isSyncEnabled);
		scheduler.AdvanceBy(TimeSpan.FromSeconds(1));

		new[] { monitor.StateStepText, monitor.LoopText, monitor.TimeLeftInStepText, monitor.TimeLeftInRecipeText }
			.Should().Equal(Resources.ExecutionNoData, string.Empty, "—", "—");
		monitor.IsRecipeActive.Should().BeFalse("the lock shows only for a run the PLC reports over a live link");
		monitor.ExecutionToolTip.Should().BeNull();
	}

	[AvaloniaFact]
	public void SyncOff_WhileIdle_ShowsNoData()
	{
		Driver().AddWait(60f);
		using var monitor = CreateMonitor();

		PushLink(PlcConnectionState.Disconnected, isSyncEnabled: false);

		new[] { monitor.StateStepText, monitor.TimeLeftInRecipeText }.Should().Equal(Resources.ExecutionNoData, "—");
	}

	[AvaloniaFact]
	public void WindowTitle_DropsTheRunningSuffix_WhenTheLinkDropsMidRun()
	{
		Driver().AddWait();
		var viewModel = _fixture.CreateMainWindowViewModel();
		Push(Running(line: 0));

		PushLink(PlcConnectionState.Disconnected, isSyncEnabled: true);

		viewModel.WindowTitle.Should().NotEndWith(Resources.WindowTitleRunningSuffix);

		viewModel.Dispose();
	}

	private PlcMonitorViewModel CreateMonitor(IScheduler? scheduler = null)
	{
		return new PlcMonitorViewModel(
			_fixture.Coordinator,
			_fixture.RecipeMetadataRegistry,
			scheduler ?? new HistoricalScheduler());
	}

	private void PushLink(PlcConnectionState link, bool isSyncEnabled)
	{
		_fixture.PlcSyncService.PushPlcState(new PlcSessionSnapshot(link, PlcSyncStatus.Idle, isSyncEnabled));
		Dispatcher.UIThread.RunJobs();
	}

	private RecipeTestDriver Driver()
	{
		return new RecipeTestDriver(_fixture.Session).NewRecipe();
	}

	private void NestedLoopRecipe()
	{
		Driver().AddFor(5).AddFor(3).AddWait().AddEndFor().AddEndFor().AddWait().AddWait().AddWait();
	}

	private void Push(PlcExecutionInfo info)
	{
		_fixture.StubS7.PushExecutionState(info);
		Dispatcher.UIThread.RunJobs();
	}

	private static PlcExecutionInfo Running(int line, int depth1 = 0, int depth2 = 0)
	{
		return new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: line,
			StepCurrentTime: 0f,
			ForLoopCount1: depth1,
			ForLoopCount2: depth2,
			ForLoopCount3: 0);
	}

	private static string RunningText(string step, int total)
	{
		return string.Format(CultureInfo.InvariantCulture, Resources.ExecutionRunningFormat, step, total);
	}

	private static string Format(string format, params object[] values)
	{
		return string.Format(CultureInfo.InvariantCulture, format, values);
	}
}
