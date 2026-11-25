using System.Reactive.Concurrency;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.Localization;
using SemiStep.UI.Plc;

using Xunit;

namespace SemiStep.Tests.UI.Plc;

[Trait("Component", "UI")]
[Trait("Area", "StatusBar")]
[Trait("Category", "Unit")]
public sealed class TimerTextTests : IAsyncLifetime
{
	private const string Dash = "—";

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
	public void EmptyRecipe_ShowsDashForBoth()
	{
		Driver();
		using var monitor = CreateMonitor();

		Timers(monitor).Should().Equal(Resources.StatusStepLabel, Dash, Resources.StatusRecipeLabel, Dash);
	}

	[AvaloniaFact]
	public void Idle_ShowsNoStepTime_AndTheRecipeTotal()
	{
		Driver().AddWait(60f).AddWait(30f);
		using var monitor = CreateMonitor();

		Timers(monitor).Should().Equal(Resources.StatusStepLabel, Dash, Resources.RecipeTimerIdleCaption, "00:01:30");
	}

	[AvaloniaFact]
	public void Running_ShowsTheTimeLeft()
	{
		Driver().AddWait(100f).AddWait(50f);
		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 0,
			StepCurrentTime: 25f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0));
		Dispatcher.UIThread.RunJobs();

		Timers(monitor).Should().Equal(
			Resources.StepTimerRunningCaption, "00:01:15", Resources.RecipeTimerRunningCaption, "00:02:05");
	}

	private PlcMonitorViewModel CreateMonitor()
	{
		return new PlcMonitorViewModel(_fixture.Coordinator, _fixture.RecipeMetadataRegistry, new HistoricalScheduler());
	}

	private RecipeTestDriver Driver()
	{
		return new RecipeTestDriver(_fixture.Session).NewRecipe();
	}

	private static string[] Timers(PlcMonitorViewModel monitor)
	{
		return
		[
			monitor.TimeLeftInStepCaption,
			monitor.TimeLeftInStepText,
			monitor.TimeLeftInRecipeCaption,
			monitor.TimeLeftInRecipeText
		];
	}
}
