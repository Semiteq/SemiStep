using System.Collections.Immutable;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes.Analysis;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "Timings")]
public sealed class ExecutionTimeEstimatorTests
{
	private const int ImmediateActionId = 100;
	private const int LongLastingActionId = 200;
	private const string StepDurationKey = "step_duration";

	private static RecipeMetadataRegistry BuildRegistry()
	{
		var actions = new Dictionary<int, ActionDefinition>
		{
			[ImmediateActionId] = new ActionDefinition(
				id: ImmediateActionId,
				uiName: "ImmediateAction",
				deployDuration: DeployDuration.Immediate,
				properties:
				[
					new ActionPropertyDefinition(
						Key: StepDurationKey,
						GroupName: null,
						PropertyTypeId: "time",
						DefaultValue: "0")
				]),
			[LongLastingActionId] = new ActionDefinition(
				id: LongLastingActionId,
				uiName: "LongLastingAction",
				deployDuration: DeployDuration.LongLasting,
				properties:
				[
					new ActionPropertyDefinition(
						Key: StepDurationKey,
						GroupName: null,
						PropertyTypeId: "time",
						DefaultValue: "0")
				])
		};

		var config = new AppConfiguration(
			Properties: TestRecipeMetadataRegistryFactory.DefaultStringProperty(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: new AppUiOptions("ru"));

		return new RecipeMetadataRegistry(config);
	}

	private static Step BuildStep(int actionId, float durationSeconds)
	{
		return new Step(
			actionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(StepDurationKey), PropertyValue.FromFloat(durationSeconds)));
	}

	private static RecipeSnapshot BuildSnapshot(
		Recipe recipe,
		IReadOnlyList<LoopInfo> loops,
		RecipeMetadataRegistry registry)
	{
		var (startTimes, total, singleIterations) = TimingCalculator.Calculate(recipe, loops, registry);
		return RecipeSnapshot.Create(recipe, total, startTimes, loops, singleIterations);
	}

	private static RecipeSnapshot BuildSingleLoopSnapshot()
	{
		// Layout: [For x3] body(10) [End_For], total 30 sec
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(ImmediateActionId, 0f), // 0: For
			BuildStep(LongLastingActionId, 10f), // 1: body
			BuildStep(ImmediateActionId, 0f))); // 2: End_For
		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 2, Depth: 1, Iterations: 3)
		};
		return BuildSnapshot(recipe, loops, BuildRegistry());
	}

	private static RecipeSnapshot BuildReferenceSnapshot()
	{
		// Layout: wait(2) [For x3] wait(2) [For x5] wait(2) wait(2) [End_For] [End_For] wait(2)
		// inner pass 4 sec, outer pass 2 + 5 * 4 = 22 sec, total 2 + 3 * 22 + 2 = 70 sec
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(LongLastingActionId, 2f), // 0
			BuildStep(ImmediateActionId, 0f), // 1: outer For
			BuildStep(LongLastingActionId, 2f), // 2
			BuildStep(ImmediateActionId, 0f), // 3: inner For
			BuildStep(LongLastingActionId, 2f), // 4
			BuildStep(LongLastingActionId, 2f), // 5
			BuildStep(ImmediateActionId, 0f), // 6: inner End_For
			BuildStep(ImmediateActionId, 0f), // 7: outer End_For
			BuildStep(LongLastingActionId, 2f))); // 8
		var loops = new[]
		{
			new LoopInfo(StartIndex: 1, EndIndex: 7, Depth: 1, Iterations: 3),
			new LoopInfo(StartIndex: 3, EndIndex: 6, Depth: 2, Iterations: 5)
		};
		return BuildSnapshot(recipe, loops, BuildRegistry());
	}

	private static PlcExecutionInfo Info(
		int actualLine,
		float stepCurrentTime,
		int count1 = 0,
		int count2 = 0,
		int count3 = 0,
		bool active = true)
	{
		return new PlcExecutionInfo(
			RecipeActive: active,
			ActualLine: actualLine,
			StepCurrentTime: stepCurrentTime,
			ForLoopCount1: count1,
			ForLoopCount2: count2,
			ForLoopCount3: count3);
	}

	[Fact]
	public void TimeLeftInRecipe_LinearRecipeAtStart_EqualsTotalDuration()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(LongLastingActionId, 10f),
			BuildStep(LongLastingActionId, 20f)));
		var snapshot = BuildSnapshot(recipe, [], registry);

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(0, 0f));

		result.Should().Be(snapshot.TotalDuration);
	}

	[Fact]
	public void TimeLeftInRecipe_ActualLineBeyondStartTimes_ReturnsZeroLikeMissingKey()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe([BuildStep(LongLastingActionId, 10f)]);
		var snapshot = BuildSnapshot(recipe, [], registry);

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(5, 0f));

		result.Should().Be(TimeSpan.Zero,
			"an out-of-range step index yields the same zero the missing dictionary key gave");
	}

	[Fact]
	public void TimeLeftInRecipe_NegativeActualLine_ReturnsZeroLikeMissingKey()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe([BuildStep(LongLastingActionId, 10f)]);
		var snapshot = BuildSnapshot(recipe, [], registry);

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(-1, 0f));

		result.Should().Be(TimeSpan.Zero,
			"a negative step index must not index the start-time array; it yields the same zero the "
			+ "missing dictionary key gave");
	}

	[Fact]
	public void TimeLeftInRecipe_LinearRecipeAtLastStepFinished_IsZero()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(LongLastingActionId, 10f),
			BuildStep(LongLastingActionId, 20f)));
		var snapshot = BuildSnapshot(recipe, [], registry);

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(1, 20f));

		result.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void TimeLeftInRecipe_SingleLoop_SecondPass_RecipeRemainderShrinksByOnePass()
	{
		var snapshot = BuildSingleLoopSnapshot();

		var atFirstPass = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(1, 0f, count1: 1));
		var atSecondPass = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(1, 0f, count1: 2));

		atFirstPass.Should().Be(TimeSpan.FromSeconds(30));
		atSecondPass.Should().Be(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void TimeLeftInRecipe_LoopCounterZero_CountsLikeFirstPass()
	{
		var snapshot = BuildSingleLoopSnapshot();

		var atZero = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(1, 0f, count1: 0));
		var atFirstPass = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(1, 0f, count1: 1));

		atZero.Should().Be(atFirstPass);
	}

	[Fact]
	public void TimeLeftInRecipe_GarbageCounterAboveIterations_BoundsAtLastPass()
	{
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(ImmediateActionId, 0f),
			BuildStep(LongLastingActionId, 600f),
			BuildStep(ImmediateActionId, 0f)));
		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 2, Depth: 1, Iterations: 3)
		};
		var snapshot = BuildSnapshot(recipe, loops, BuildRegistry());

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(1, 0f, count1: int.MaxValue));

		result.Should().Be(TimeSpan.FromSeconds(600));
	}

	[Fact]
	public void TimeLeftInRecipe_ReferenceRecipe_TotalIsSeventySeconds()
	{
		var snapshot = BuildReferenceSnapshot();

		snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(70));
	}

	[Theory]
	// 0-based; plan lines 6, 5, 7, 8 ...
	[InlineData(5, 1, 5, 48)]
	[InlineData(4, 2, 2, 40)]
	[InlineData(6, 1, 5, 46)]
	[InlineData(6, 2, 5, 24)]
	[InlineData(7, 0, 0, 46)]
	[InlineData(7, 3, 0, 2)]
	public void TimeLeftInRecipe_ReferenceRecipe_CountsPassesBeforeCurrentOne(
		int actualLineIndex,
		int outerPass,
		int innerPass,
		int expectedSecondsLeft)
	{
		var snapshot = BuildReferenceSnapshot();

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(
			snapshot,
			Info(actualLineIndex, 0f, count1: outerPass, count2: innerPass));

		result.Should().Be(TimeSpan.FromSeconds(expectedSecondsLeft));
	}

	[Fact]
	public void TimeLeftInRecipe_NestedLoops_BothCountersContribute()
	{
		var registry = BuildRegistry();
		const float OuterStepSeconds = 4f;
		const float InnerStepSeconds = 3f;
		const int InnerIterations = 3;
		const int OuterIterations = 2;

		// Layout: [For outer] step(4) [For inner] step(3) [End_For inner] [End_For outer]
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(ImmediateActionId, 0f),   // 0: outer For
			BuildStep(LongLastingActionId, OuterStepSeconds), // 1: outer body
			BuildStep(ImmediateActionId, 0f),   // 2: inner For
			BuildStep(LongLastingActionId, InnerStepSeconds), // 3: inner body
			BuildStep(ImmediateActionId, 0f),   // 4: inner End_For
			BuildStep(ImmediateActionId, 0f))); // 5: outer End_For
		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 5, Depth: 1, Iterations: OuterIterations),
			new LoopInfo(StartIndex: 2, EndIndex: 4, Depth: 2, Iterations: InnerIterations)
		};
		var snapshot = BuildSnapshot(recipe, loops, registry);

		// Step 3 (inner body), outer pass 2, inner pass 3
		var info = Info(3, stepCurrentTime: 0f, count1: 2, count2: 3);
		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, info);

		// stepStart[3] = 4 sec (first-pass accounting); outer pass = 4 + 3 * 3 = 13 sec; inner pass = 3 sec
		// consumed = 4 + (2 - 1) * 13 + (3 - 1) * 3 = 23 sec of a 26 sec total
		snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(26));
		result.Should().Be(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void TimeLeftInRecipe_TwoSequentialLoops_CurrentInSecond_UsesSecondLoopSingleIterationOnly()
	{
		var registry = BuildRegistry();
		const float FirstBodySeconds = 5f;
		const float SecondBodySeconds = 7f;

		// Layout: [For1] body(5) [End_For1] [For2] body(7) [End_For2]
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(ImmediateActionId, 0f),                 // 0: For#1
			BuildStep(LongLastingActionId, FirstBodySeconds), // 1: body#1
			BuildStep(ImmediateActionId, 0f),                 // 2: End_For#1
			BuildStep(ImmediateActionId, 0f),                 // 3: For#2
			BuildStep(LongLastingActionId, SecondBodySeconds),// 4: body#2
			BuildStep(ImmediateActionId, 0f)));               // 5: End_For#2
		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 2, Depth: 1, Iterations: 2),
			new LoopInfo(StartIndex: 3, EndIndex: 5, Depth: 1, Iterations: 4)
		};
		var snapshot = BuildSnapshot(recipe, loops, registry);

		// In the second loop body; the first loop is NOT in EnclosingLoops for index 4.
		// total = 2 * 5 + 4 * 7 = 38 sec; stepStart[4] = 10 sec
		var atFirstPass = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(4, 0f, count1: 1));
		var atSecondPass = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(4, 0f, count1: 2));

		atFirstPass.Should().Be(TimeSpan.FromSeconds(28));
		(atFirstPass - atSecondPass).Should().Be(TimeSpan.FromSeconds(SecondBodySeconds),
			"only the second loop's single-pass duration should be subtracted, never the first loop's");
	}

	[Fact]
	public void TimeLeftInRecipe_ActualLineBeyondRecipe_ReturnsZero()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe([BuildStep(LongLastingActionId, 10f)]);
		var snapshot = BuildSnapshot(recipe, [], registry);

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(99, 0f));

		result.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void TimeLeftInStep_ElapsedExceedsDuration_ClampsToZero()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe([BuildStep(LongLastingActionId, 10f)]);
		var snapshot = BuildSnapshot(recipe, [], registry);

		var result = ExecutionTimeEstimator.TimeLeftInStep(snapshot, Info(0, 999f), registry);

		result.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void TimeLeftInStep_ActualLineBeyondRecipe_ReturnsZero()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe([BuildStep(LongLastingActionId, 10f)]);
		var snapshot = BuildSnapshot(recipe, [], registry);

		var result = ExecutionTimeEstimator.TimeLeftInStep(snapshot, Info(5, 0f), registry);

		result.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void TimeLeftInStep_AtMidStep_ReturnsRemainder()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe([BuildStep(LongLastingActionId, 10f)]);
		var snapshot = BuildSnapshot(recipe, [], registry);

		var result = ExecutionTimeEstimator.TimeLeftInStep(snapshot, Info(0, 4f), registry);

		result.Should().Be(TimeSpan.FromSeconds(6));
	}
}
