using AwesomeAssertions;

using FluentResults;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tools.PlcEmulator;

using Xunit;

using static SemiStep.Tests.Emulator.RunEngineHarness;

namespace SemiStep.Tests.Emulator;

[Trait("Component", "S7")]
[Trait("Area", "Emulator")]
[Trait("Category", "Integration")]
public sealed class RunEngineTests
{
	private const int HoldTicks = 10;

	[Fact]
	public async Task Run_NothingCommitted_RefusesAndLeavesExecutionAreaIdle()
	{
		using var harness = await CreateAsync();

		var result = harness.Engine.Run();

		ReasonOf(result).Should().Be(RunRefusalReason.NoCommittedRecipe);
		harness.Tick().RecipeActive.Should().BeFalse();
	}

	[Fact]
	public async Task Run_DeclaredLinesDifferFromStoredData_RefusesWithDecodeReason()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(1f), Wait(1f), Wait(1f)), declaredLines: 2);

		var result = harness.Engine.Run();

		ReasonOf(result).Should().Be(RunRefusalReason.RecipeDoesNotDecode);
		harness.Tick().RecipeActive.Should().BeFalse();
	}

	[Theory]
	[MemberData(nameof(InvalidLoopRecipes))]
	public async Task Run_RecipeFailsAnalysis_RefusesWithAnalysisReason(Recipe recipe)
	{
		using var harness = await CreateAsync();
		harness.Commit(recipe);

		var result = harness.Engine.Run();

		ReasonOf(result).Should().Be(RunRefusalReason.RecipeFailsAnalysis);
		harness.Tick().RecipeActive.Should().BeFalse();
	}

	[Fact]
	public async Task Run_ValidRecipe_ShowsLineMinusOneForOneTickThenEachLineForItsDuration()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(0.3f), Wait(0f), Wait(0.2f)));

		harness.Engine.Run().IsSuccess.Should().BeTrue();
		var started = harness.ReadExecution();
		var observed = Enumerable.Range(0, 7).Select(_ => harness.Tick()).ToList();

		started.Should().Be(new PlcExecutionInfo(true, -1, 0f, 0, 0, 0));
		observed.Select(state => (state.ActualLine, MathF.Round(state.StepCurrentTime, 1))).Should().Equal(
			(0, 0f),
			(0, 0.1f),
			(0, 0.2f),
			(1, 0f),
			(2, 0f),
			(2, 0.1f),
			(3, 0f));
		observed.Should().OnlyContain(state => state.RecipeActive);
	}

	[Fact]
	public async Task State_ThreeStepTimeline_ReportsPhaseAndOneBasedLineOnEachTick()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(0.1f), Pause(), Wait(0.1f)));
		var observed = new List<(RunPhase Phase, int Line)>();

		void Observe()
		{
			var state = harness.Engine.State;
			observed.Add((state.Phase, state.DisplayLine));
		}

		Observe();
		harness.Engine.Run();
		Observe();
		harness.Tick();
		Observe();
		harness.Tick();
		Observe();
		harness.Tick(3);
		Observe();
		harness.Engine.Continue();
		harness.Tick();
		Observe();
		harness.Tick();
		Observe();
		harness.Tick(HoldTicks);
		Observe();

		observed.Should().Equal(
			(RunPhase.Idle, 0),
			(RunPhase.Starting, 1),
			(RunPhase.Running, 1),
			(RunPhase.Paused, 2),
			(RunPhase.Paused, 2),
			(RunPhase.Running, 3),
			(RunPhase.Finishing, 3),
			(RunPhase.Idle, 3));
	}

	[Fact]
	public async Task State_StopDuringRun_FinishesAtTheStoppedLine()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(5f), Wait(5f)));
		harness.Engine.Run();
		harness.Tick(2);

		harness.Engine.Stop();
		var finishing = harness.Engine.State;
		harness.Tick(HoldTicks);

		(finishing.Phase, finishing.DisplayLine, finishing.LineCount).Should().Be((RunPhase.Finishing, 1, 2));
		harness.Engine.State.IsActive.Should().BeFalse();
	}

	[Fact]
	public async Task State_StopBeforeTheFirstLine_ClampsTheLineToOne()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(5f), Wait(5f)));
		harness.Engine.Run();

		harness.Engine.Stop();

		harness.Engine.State.Should().Be(new RunState(RunPhase.Finishing, -1, 2));
		harness.Engine.State.DisplayLine.Should().Be(1);
	}

	[Fact]
	public async Task Tick_RecipeCommitted_ReportsTheCommitOnce()
	{
		using var harness = await CreateAsync();
		harness.Loop.Tick();
		harness.Commit(RecipeOf(Wait(0.1f), Wait(0.1f), Wait(0.1f)));

		harness.Loop.Tick();
		harness.Loop.Tick();

		harness.Events.Should().Equal(new RecipeCommitted(3));
	}

	[Fact]
	public async Task Run_AfterLastLine_KeepsLineCountAndHoldsRecipeActiveForOneSecond()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(0.1f), Wait(0.1f)));
		harness.Engine.Run();
		var lastLine = harness.Tick(2);

		var finished = harness.Tick();
		var holding = harness.Tick(HoldTicks - 1);
		var cleared = harness.Tick();

		lastLine.ActualLine.Should().Be(1);
		finished.Should().Be(new PlcExecutionInfo(true, 2, 0f, 0, 0, 0));
		holding.Should().Be(finished);
		cleared.Should().Be(new PlcExecutionInfo(false, 2, 0f, 0, 0, 0));
		harness.Tick(5).Should().Be(cleared);
	}

	[Fact]
	public async Task Run_SingleLoop_ShowsForOnceAndEndForEachIterationWithOneBasedCount()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(For(2), Wait(0.1f), EndFor()));
		harness.Engine.Run();

		var observed = Enumerable.Range(0, 5).Select(_ => harness.Tick()).ToList();

		observed.Select(Describe).Should().Equal(
			(0, 1, 0, 0),
			(1, 1, 0, 0),
			(2, 1, 0, 0),
			(1, 2, 0, 0),
			(2, 2, 0, 0));
		Describe(harness.Tick()).Should().Be((3, 0, 0, 0));
	}

	[Fact]
	public async Task Run_NestedLoops_CountsIterationPerDepthAndZeroesFinishedDepth()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(For(2), For(2), Wait(0.1f), EndFor(), EndFor()));
		harness.Engine.Run();

		var observed = Enumerable.Range(0, 13).Select(_ => harness.Tick()).ToList();

		observed.Select(Describe).Should().Equal(
			(0, 1, 0, 0),
			(1, 1, 1, 0),
			(2, 1, 1, 0),
			(3, 1, 1, 0),
			(2, 1, 2, 0),
			(3, 1, 2, 0),
			(4, 1, 0, 0),
			(1, 2, 1, 0),
			(2, 2, 1, 0),
			(3, 2, 1, 0),
			(2, 2, 2, 0),
			(3, 2, 2, 0),
			(4, 2, 0, 0));
	}

	[Fact]
	public async Task Run_ThreeNestedLoops_FillsAllThreeCounters()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(For(1), For(1), For(1), Wait(0.1f), EndFor(), EndFor(), EndFor()));
		harness.Engine.Run();

		var observed = Enumerable.Range(0, 7).Select(_ => harness.Tick()).ToList();

		observed.Select(Describe).Should().Equal(
			(0, 1, 0, 0),
			(1, 1, 1, 0),
			(2, 1, 1, 1),
			(3, 1, 1, 1),
			(4, 1, 1, 1),
			(5, 1, 1, 0),
			(6, 1, 0, 0));
	}

	[Fact]
	public async Task Run_PauseStep_HoldsLineWhileStepTimeCountsUntilContinue()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Pause(), Wait(0.1f)));
		harness.Engine.Run();
		harness.Tick();

		var held = harness.Tick(20);
		harness.Engine.Continue().IsSuccess.Should().BeTrue();
		var afterContinue = harness.Tick();

		Describe(held).Should().Be((0, 0, 0, 0));
		held.StepCurrentTime.Should().BeApproximately(2.0f, 0.001f);
		Describe(afterContinue).Should().Be((1, 0, 0, 0));
		afterContinue.StepCurrentTime.Should().Be(0f);
	}

	[Fact]
	public async Task Continue_NotWaitingAtPause_Refuses()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(5f)));

		var idle = harness.Engine.Continue();
		harness.Engine.Run();
		harness.Tick(2);
		var running = harness.Engine.Continue();

		ReasonOf(idle).Should().Be(RunRefusalReason.NotPaused);
		ReasonOf(running).Should().Be(RunRefusalReason.NotPaused);
	}

	[Fact]
	public async Task Run_WhileRunning_RefusesWithAlreadyRunningAndChangesNothing()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(5f)));
		harness.Engine.Run();
		var before = harness.Tick(3);

		var second = harness.Engine.Run();

		ReasonOf(second).Should().Be(RunRefusalReason.AlreadyRunning);
		harness.ReadExecution().Should().Be(before);
	}

	[Fact]
	public async Task Stop_WhileRunning_HoldsRecipeActiveForOneSecondThenClears()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(5f), Wait(5f)));
		harness.Engine.Run();
		harness.Tick(4);

		var stopped = harness.Engine.Stop();
		var afterStop = harness.ReadExecution();
		var holding = harness.Tick(HoldTicks - 1);
		var cleared = harness.Tick();

		stopped.IsSuccess.Should().BeTrue();
		afterStop.Should().Be(new PlcExecutionInfo(true, 0, 0f, 0, 0, 0));
		holding.RecipeActive.Should().BeTrue();
		cleared.RecipeActive.Should().BeFalse();
		harness.Engine.Run().IsSuccess.Should().BeTrue();
	}

	[Fact]
	public async Task Stop_NothingRunning_Refuses()
	{
		using var harness = await CreateAsync();

		var result = harness.Engine.Stop();

		ReasonOf(result).Should().Be(RunRefusalReason.NotRunning);
	}

	[Fact]
	public async Task Stop_DuringTheFinishingHold_RefusesAndKeepsTheHold()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(5f)));
		harness.Engine.Run();
		harness.Tick(2);
		harness.Engine.Stop();
		var holding = harness.Tick();

		var second = harness.Engine.Stop();

		ReasonOf(second).Should().Be(RunRefusalReason.NotRunning);
		harness.Tick(HoldTicks - 2).Should().Be(holding);
	}

	[Fact]
	public async Task Run_AppRewritesRecipeMidRun_ContinuesOnTheCopyTakenAtRun()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(0.1f), Wait(0.1f), Wait(0.1f)));
		harness.Engine.Run();
		harness.Tick(2);

		harness.Commit(RecipeOf(Wait(0.1f)));
		var observed = Enumerable.Range(0, 3).Select(_ => harness.Tick()).ToList();

		observed.Select(state => state.ActualLine).Should().Equal(2, 3, 3);
	}

	[Fact]
	public async Task Loop_RunsOnTimeProvider_AdvancingTheClockAdvancesTheRun()
	{
		using var harness = await CreateAsync();
		harness.Commit(RecipeOf(Wait(5f)));
		using var stop = new CancellationTokenSource();
		var loop = harness.Loop.RunAsync(harness.Time, stop.Token);
		harness.Engine.Run();

		harness.Time.Advance(TimeSpan.FromMilliseconds(100));
		await WaitUntilAsync(() => harness.ReadExecution() is { RecipeActive: true, ActualLine: 0 });
		harness.Time.Advance(TimeSpan.FromMilliseconds(100));
		await WaitUntilAsync(() => Math.Abs(harness.ReadExecution().StepCurrentTime - 0.1f) < 0.001f);

		await stop.CancelAsync();
		await loop;
	}

	public static TheoryData<Recipe> InvalidLoopRecipes()
	{
		return
		[
			RecipeOf(For(2), Wait(0.1f)),
			RecipeOf(Wait(0.1f), EndFor()),
			RecipeOf(For(0), Wait(0.1f), EndFor()),
			RecipeOf(For(-3), Wait(0.1f), EndFor()),
			RecipeOf(For(1), For(1), For(1), For(1), Wait(0.1f), EndFor(), EndFor(), EndFor(), EndFor())
		];
	}

	private static (int Line, int First, int Second, int Third) Describe(PlcExecutionInfo state)
	{
		return (state.ActualLine, state.ForLoopCount1, state.ForLoopCount2, state.ForLoopCount3);
	}

	private static RunRefusalReason? ReasonOf(IResultBase result)
	{
		return result.Errors.OfType<RunRefusal>().SingleOrDefault()?.Reason;
	}

	private static Task WaitUntilAsync(Func<bool> condition)
	{
		return TestHelpers.WaitUntilAsync(
			condition,
			TimeSpan.FromSeconds(10),
			TestHelpers.DefaultPollInterval,
			TestContext.Current.CancellationToken);
	}
}
