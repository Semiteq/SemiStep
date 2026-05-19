using System.Collections.Immutable;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;

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
			Properties: new Dictionary<string, PropertyTypeDefinition>(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default);

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

		var (stepStartTimes, totalDuration) = TimingCalculator.Calculate(
			recipe,
			Array.Empty<LoopInfo>(),
			registry);

		stepStartTimes[0].Should().Be(TimeSpan.Zero);
		stepStartTimes[1].Should().Be(TimeSpan.Zero,
			"the immediate first step contributes nothing to cumulative time");
		totalDuration.Should().Be(TimeSpan.FromSeconds(20));
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

		var (_, totalDuration) = TimingCalculator.Calculate(recipe, loops, registry);

		var expectedDeltaPerIteration = TimeSpan.FromSeconds(LongLastingDurationSeconds);
		totalDuration.Should().Be(expectedDeltaPerIteration * Iterations);
	}
}
