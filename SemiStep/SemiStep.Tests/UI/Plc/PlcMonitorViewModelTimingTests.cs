using System.Globalization;
using System.Reactive.Concurrency;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Plc.State;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Plc;

using Xunit;

namespace SemiStep.Tests.UI.Plc;

[Trait("Component", "UI")]
[Trait("Area", "Timings")]
[Trait("Category", "Unit")]
public sealed class PlcMonitorViewModelTimingTests : IAsyncLifetime
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

	private PlcMonitorViewModel CreateMonitor()
	{
		return new PlcMonitorViewModel(
			_fixture.Coordinator,
			_fixture.RecipeMetadataRegistry,
			new HistoricalScheduler());
	}

	private RecipeTestDriver Driver()
	{
		return new RecipeTestDriver(_fixture.Session).NewRecipe();
	}

	private static PlcExecutionInfo State(bool active, int line, float stepTime, int c1 = 0, int c2 = 0, int c3 = 0)
	{
		return new PlcExecutionInfo(
			RecipeActive: active,
			ActualLine: line,
			StepCurrentTime: stepTime,
			ForLoopCount1: c1,
			ForLoopCount2: c2,
			ForLoopCount3: c3);
	}

	[AvaloniaFact]
	public void Initially_EmptySnapshot_ShowsDash()
	{
		Driver();
		using var monitor = CreateMonitor();

		monitor.TimeLeftInStepText.Should().Be("—");
		monitor.TimeLeftInRecipeText.Should().Be("—");
	}

	[AvaloniaFact]
	public void Idle_WithLoadedRecipe_ShowsZeroStepAndTotalDuration()
	{
		Driver().AddWait(60f).AddWait(30f);

		using var monitor = CreateMonitor();

		monitor.TimeLeftInStepText.Should().Be("00:00:00");
		monitor.TimeLeftInRecipeText.Should().Be("00:01:30");
	}

	[AvaloniaFact]
	public void Active_FormatsHoursMinutesSeconds()
	{
		Driver().AddWait(100f).AddWait(50f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(State(true, 0, 25f));

		monitor.TimeLeftInStepText.Should().Be("00:01:15");
		monitor.TimeLeftInRecipeText.Should().Be("00:02:05");
	}

	[AvaloniaFact]
	public void Active_LongRecipe_FormatsWithDays()
	{
		Driver().AddWait(60f * 60f * 23f).AddWait(60f * 60f * 23f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(State(true, 0, 0f));

		monitor.TimeLeftInStepText.Should().Be("23:00:00");
		monitor.TimeLeftInRecipeText.Should().Be("1.22:00:00");
	}

	[AvaloniaFact]
	public void MonotonicClamp_HoldsPreviousElapsedOnBackwardJitter()
	{
		Driver().AddWait(100f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(State(true, 0, 40f));
		var afterFirst = monitor.TimeLeftInStepText;

		_fixture.StubS7.PushExecutionState(State(true, 0, 30f));

		afterFirst.Should().Be("00:01:00");
		monitor.TimeLeftInStepText.Should().Be("00:01:00");
	}

	[AvaloniaFact]
	public void MonotonicClamp_ResetsOnActiveInactiveActiveTransition()
	{
		Driver().AddWait(100f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(State(true, 0, 60f));
		_fixture.StubS7.PushExecutionState(State(false, 0, 0f));
		_fixture.StubS7.PushExecutionState(State(true, 0, 10f));

		// New active session: stale clamp baseline (60s) must not hold; we must
		// see the fresh 10s value reflected in the remaining time.
		monitor.TimeLeftInStepText.Should().Be("00:01:30");
	}

	[AvaloniaFact]
	public void HoldLastGood_ActiveToInactive_ResetsToIdleMapping()
	{
		Driver().AddWait(60f).AddWait(60f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(State(true, 0, 10f));
		_fixture.StubS7.PushExecutionState(State(false, 0, 0f));

		monitor.TimeLeftInStepText.Should().Be("00:00:00");
		monitor.TimeLeftInRecipeText.Should().Be("00:02:00");
	}

	[AvaloniaFact]
	public void ActiveFalseTrueFalse_ResetsToIdleMappingAfterSecondFalse()
	{
		Driver().AddWait(45f).AddWait(15f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(State(false, 0, 0f));
		_fixture.StubS7.PushExecutionState(State(true, 0, 20f));
		_fixture.StubS7.PushExecutionState(State(false, 0, 0f));

		monitor.TimeLeftInStepText.Should().Be("00:00:00");
		monitor.TimeLeftInRecipeText.Should().Be("00:01:00");
	}

	[AvaloniaFact]
	public void Interpolation_NoOpWhenInactive()
	{
		Driver().AddWait(60f);

		var scheduler = new HistoricalScheduler();
		using var monitor = new PlcMonitorViewModel(
			_fixture.Coordinator,
			_fixture.RecipeMetadataRegistry,
			scheduler);

		var before = monitor.TimeLeftInRecipeText;
		scheduler.AdvanceBy(TimeSpan.FromSeconds(5));

		before.Should().Be("00:01:00");
		monitor.TimeLeftInRecipeText.Should().Be("00:01:00");
	}

	[AvaloniaFact]
	public void Interpolation_AdvancesElapsedWhileActive()
	{
		Driver().AddWait(60f);

		var scheduler = new HistoricalScheduler();
		using var monitor = new PlcMonitorViewModel(
			_fixture.Coordinator,
			_fixture.RecipeMetadataRegistry,
			scheduler);

		_fixture.StubS7.PushExecutionState(State(true, 0, 0f));

		scheduler.AdvanceBy(TimeSpan.FromSeconds(5));

		// At t=5s of interpolation, step elapsed should be exactly 5s,
		// so remaining is 60 - 5 = 55s.
		monitor.TimeLeftInStepText.Should().Be("00:00:55");
	}

	[AvaloniaFact]
	public void ExecutionState_PropagatesActualLineAndForLoopCounts()
	{
		Driver().AddWait(60f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(State(true, line: 0, stepTime: 5f, c1: 2, c2: 3, c3: 4));

		monitor.IsRecipeActive.Should().BeTrue();
		monitor.ActualLine.Should().Be(0);
		monitor.ForLoopCount1.Should().Be(2);
		monitor.ForLoopCount2.Should().Be(3);
		monitor.ForLoopCount3.Should().Be(4);
	}

	[AvaloniaFact]
	public void CoordinatorMutated_UpdatesRecipeRemainingText()
	{
		Driver().AddWait(60f);
		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(State(true, 0, 0f));

		monitor.TimeLeftInRecipeText.Should().Be("00:01:00");

		// Mutate via the coordinator so its Mutated event fires.
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.UpdateStepProperty(
			_fixture.Session.Current.StepCount - 1,
			RecipeTestDriver.StepDurationColumn,
			(120f).ToString(CultureInfo.InvariantCulture));

		// New total = 60 + 120 = 180s; elapsed in current step still 0 → remaining 180s.
		monitor.TimeLeftInRecipeText.Should().Be("00:03:00");
	}

	[AvaloniaFact]
	public void IntegrationViaCoordinatorExecutionStateStream_VmObservesEvent()
	{
		// Documents that the VM subscribes to RecipeCoordinator.ExecutionState (not StubS7
		// directly). StubS7.PushExecutionState publishes through the stream that the
		// coordinator re-exposes as ExecutionState. Asserting on VM state after a push
		// proves the wiring end-to-end.
		Driver().AddWait(30f);
		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(State(true, 0, 10f));

		monitor.IsRecipeActive.Should().BeTrue();
		monitor.TimeLeftInStepText.Should().Be("00:00:20");
		monitor.TimeLeftInRecipeText.Should().Be("00:00:20");
	}
}
