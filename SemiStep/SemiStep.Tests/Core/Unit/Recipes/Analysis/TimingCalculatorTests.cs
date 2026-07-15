using System.Collections.Immutable;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes.Analysis;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "Timings")]
public sealed class TimingCalculatorTests
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

	[Fact]
	public void Calculate_ImmediateActionExcludedFromCumulativeTime()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(ImmediateActionId, 10f),
			BuildStep(LongLastingActionId, 20f)));

		var (stepStartTimes, totalDuration, singleIterations) = TimingCalculator.Calculate(
			recipe,
			Array.Empty<LoopInfo>(),
			registry);

		stepStartTimes[0].Should().Be(TimeSpan.Zero);
		stepStartTimes[1].Should().Be(TimeSpan.Zero,
			"the immediate first step contributes nothing to cumulative time");
		totalDuration.Should().Be(TimeSpan.FromSeconds(20));
		singleIterations.Should().BeEmpty("a linear recipe has no loops");
	}

	[Fact]
	public void Calculate_LoopWithImmediateAndLongLastingBody_AccumulatesOnlyLongLastingPerIteration()
	{
		var registry = BuildRegistry();
		const float LongLastingDurationSeconds = 7f;
		const int Iterations = 4;

		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(ImmediateActionId, 5f),
			BuildStep(LongLastingActionId, LongLastingDurationSeconds),
			BuildStep(ImmediateActionId, 9f)));

		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 2, Depth: 1, Iterations: Iterations)
		};

		var (_, totalDuration, singleIterations) = TimingCalculator.Calculate(recipe, loops, registry);

		var expectedDeltaPerIteration = TimeSpan.FromSeconds(LongLastingDurationSeconds);
		totalDuration.Should().Be(expectedDeltaPerIteration * Iterations);
		singleIterations.Should().ContainKey(0);
		singleIterations[0].Should().Be(expectedDeltaPerIteration);
	}

	[Fact]
	public void Calculate_NestedLoops_OuterIterationDurationAggregatesInnerLoop()
	{
		var registry = BuildRegistry();
		const float OuterStepSeconds = 4f;
		const float InnerStepSeconds = 3f;
		const int InnerIterations = 5;
		const int OuterIterations = 2;

		// Layout: [For outer] step(4) [For inner] step(3) [End_For inner] [End_For outer]
		// Use immediate actions for For/End_For boundary steps (zero duration).
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(ImmediateActionId, 0f), // index 0: outer For
			BuildStep(LongLastingActionId, OuterStepSeconds), // index 1: outer body step
			BuildStep(ImmediateActionId, 0f), // index 2: inner For
			BuildStep(LongLastingActionId, InnerStepSeconds), // index 3: inner body step
			BuildStep(ImmediateActionId, 0f), // index 4: inner End_For
			BuildStep(ImmediateActionId, 0f))); // index 5: outer End_For

		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 5, Depth: 1, Iterations: OuterIterations),
			new LoopInfo(StartIndex: 2, EndIndex: 4, Depth: 2, Iterations: InnerIterations)
		};

		var (_, totalDuration, singleIterations) = TimingCalculator.Calculate(recipe, loops, registry);

		var innerSingle = TimeSpan.FromSeconds(InnerStepSeconds);
		var innerTotalPerOuterIteration = innerSingle * InnerIterations;
		var outerSingle = TimeSpan.FromSeconds(OuterStepSeconds) + innerTotalPerOuterIteration;

		singleIterations.Should().HaveCount(2);
		singleIterations[2].Should().Be(innerSingle);
		singleIterations[0].Should().Be(outerSingle);
		totalDuration.Should().Be(outerSingle * OuterIterations);
	}

	[Fact]
	public void Calculate_ReturnsDenseStartTimesIndexedByStepIndex()
	{
		var registry = BuildRegistry();
		var recipe = new Recipe(ImmutableList.Create(
			BuildStep(LongLastingActionId, 10f),
			BuildStep(LongLastingActionId, 20f),
			BuildStep(LongLastingActionId, 30f)));

		var (startTimes, _, _) = TimingCalculator.Calculate(
			recipe,
			Array.Empty<LoopInfo>(),
			registry);

		startTimes.Should().HaveCount(recipe.Steps.Count,
			"start-times are a dense list with one slot per step");
		startTimes[0].Should().Be(TimeSpan.Zero);
		startTimes[1].Should().Be(TimeSpan.FromSeconds(10));
		startTimes[2].Should().Be(TimeSpan.FromSeconds(30));
	}
}
