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
[Trait("Area", "Timing")]
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

		_fixture.StubS7.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 0,
			StepCurrentTime: 25f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0));

		monitor.TimeLeftInStepText.Should().Be("00:01:15");
		monitor.TimeLeftInRecipeText.Should().Be("00:02:05");
	}

	[AvaloniaFact]
	public void Active_LongRecipe_FormatsWithDays()
	{
		// step_duration is capped at 86400s (24h) per config — build a >24h recipe
		// from two near-max steps so the recipe-remaining text crosses the day boundary.
		Driver().AddWait(60f * 60f * 23f).AddWait(60f * 60f * 23f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 0,
			StepCurrentTime: 0f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0));

		monitor.TimeLeftInStepText.Should().Be("23:00:00");
		monitor.TimeLeftInRecipeText.Should().Be("1.22:00:00");
	}

	[AvaloniaFact]
	public void MonotonicClamp_HoldsPreviousElapsedOnBackwardJitter()
	{
		Driver().AddWait(100f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 0,
			StepCurrentTime: 40f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0));
		var afterFirst = monitor.TimeLeftInStepText;

		_fixture.StubS7.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 0,
			StepCurrentTime: 30f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0));

		afterFirst.Should().Be("00:01:00");
		monitor.TimeLeftInStepText.Should().Be("00:01:00");
	}

	[AvaloniaFact]
	public void HoldLastGood_ActiveToInactive_ResetsToIdleMapping()
	{
		Driver().AddWait(60f).AddWait(60f);

		using var monitor = CreateMonitor();

		_fixture.StubS7.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 0,
			StepCurrentTime: 10f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0));
		_fixture.StubS7.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: false,
			ActualLine: 0,
			StepCurrentTime: 0f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0));

		monitor.TimeLeftInStepText.Should().Be("00:00:00");
		monitor.TimeLeftInRecipeText.Should().Be("00:02:00");
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

		_fixture.StubS7.PushExecutionState(new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: 0,
			StepCurrentTime: 0f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0));

		// Allow wall-clock to advance so the interpolation delta surfaces.
		Thread.Sleep(1100);
		scheduler.AdvanceBy(TimeSpan.FromSeconds(1));

		var step = TimeSpan.Parse(monitor.TimeLeftInStepText);
		step.Should().BeLessThan(TimeSpan.FromSeconds(60));
	}
}
