using System.Collections.Immutable;

using FluentAssertions;

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
				properties: new[]
				{
					new ActionPropertyDefinition(
						Key: StepDurationKey,
						GroupName: null,
						PropertyTypeId: "time",
						DefaultValue: "0")
				}),
			[LongLastingActionId] = new ActionDefinition(
				id: LongLastingActionId,
				uiName: "LongLastingAction",
				deployDuration: DeployDuration.LongLasting,
				properties: new[]
				{
					new ActionPropertyDefinition(
						Key: StepDurationKey,
						GroupName: null,
						PropertyTypeId: "time",
						DefaultValue: "0")
				})
		};

		var config = new AppConfiguration(
			Properties: TestRecipeMetadataRegistryFactory.DefaultStringProperty(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: AppUiOptions.Default);

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
		var snapshot = BuildSnapshot(recipe, Array.Empty<LoopInfo>(), registry);

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(0, 0f));

		result.Should().Be(snapshot.TotalDuration);
	}

	[Fact]
	public void TimeLeftInRecipe_LinearRecipeAtLastStepFinished_IsZero()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(LongLastingActionId, 10f),
			BuildStep(LongLastingActionId, 20f)));
		var snapshot = BuildSnapshot(recipe, Array.Empty<LoopInfo>(), registry);

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(1, 20f));

		result.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void TimeLeftInRecipe_SingleLoop_OneCompletedIteration_RecipeRemainderShrinksByOneIteration()
	{
		var registry = BuildRegistry();
		const float StepSeconds = 10f;
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(ImmediateActionId, 0f), // 0: For
			BuildStep(LongLastingActionId, StepSeconds), // 1: body
			BuildStep(ImmediateActionId, 0f))); // 2: End_For
		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 2, Depth: 1, Iterations: 3)
		};
		var snapshot = BuildSnapshot(recipe, loops, registry);

		var atFirstIter = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(1, 0f, count1: 0));
		var atSecondIter = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(1, 0f, count1: 1));

		(atFirstIter - atSecondIter).Should().Be(TimeSpan.FromSeconds(StepSeconds));
	}

	[Fact]
	public void TimeLeftInRecipe_NestedLoops_AllThreeCountersContribute()
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

		// Step 3 (inner body), outer completed=1, inner completed=2
		var info = Info(3, stepCurrentTime: 0f, count1: 1, count2: 2);
		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, info);

		// stepStart[3] = 4 (outer body) + 0 (inner For) = 4 sec (in first iteration accounting)
		// outer single = 4 + 3*3 = 13 sec; inner single = 3 sec
		// loopOffset = 1 * 13 + 2 * 3 = 19 sec
		// consumed = 4 + 19 + 0 = 23 sec
		// total = 13 * 2 = 26 sec
		// left = 3 sec (which is the inner body remaining + closing iterations)
		var expectedConsumed = TimeSpan.FromSeconds(4) + TimeSpan.FromSeconds(13) + TimeSpan.FromSeconds(6);
		var expected = snapshot.TotalDuration - expectedConsumed;
		result.Should().Be(expected);
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

		// In second loop body, with one iteration of second loop completed.
		// First loop is NOT in EnclosingLoops for index 4 — verifies depth-based mapping.
		var withCount = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(4, 0f, count1: 1));
		var withoutCount = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(4, 0f, count1: 0));

		(withoutCount - withCount).Should().Be(TimeSpan.FromSeconds(SecondBodySeconds),
			"only the second loop's single-iteration duration should be subtracted, never the first loop's");
	}

	[Fact]
	public void TimeLeftInRecipe_ActualLineBeyondRecipe_ReturnsZero()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(LongLastingActionId, 10f)));
		var snapshot = BuildSnapshot(recipe, Array.Empty<LoopInfo>(), registry);

		var result = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, Info(99, 0f));

		result.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void TimeLeftInStep_ElapsedExceedsDuration_ClampsToZero()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(LongLastingActionId, 10f)));
		var snapshot = BuildSnapshot(recipe, Array.Empty<LoopInfo>(), registry);

		var result = ExecutionTimeEstimator.TimeLeftInStep(snapshot, Info(0, 999f), registry);

		result.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void TimeLeftInStep_ActualLineBeyondRecipe_ReturnsZero()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(LongLastingActionId, 10f)));
		var snapshot = BuildSnapshot(recipe, Array.Empty<LoopInfo>(), registry);

		var result = ExecutionTimeEstimator.TimeLeftInStep(snapshot, Info(5, 0f), registry);

		result.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void TimeLeftInStep_AtMidStep_ReturnsRemainder()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(LongLastingActionId, 10f)));
		var snapshot = BuildSnapshot(recipe, Array.Empty<LoopInfo>(), registry);

		var result = ExecutionTimeEstimator.TimeLeftInStep(snapshot, Info(0, 4f), registry);

		result.Should().Be(TimeSpan.FromSeconds(6));
	}
}
