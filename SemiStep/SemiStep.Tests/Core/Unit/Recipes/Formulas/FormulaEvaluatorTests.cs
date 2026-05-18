using System.Collections.Immutable;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Formulas;
using SemiStep.Core.Recipes.Formulas.Errors;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes.Formulas;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "Formulas")]
public sealed class FormulaEvaluatorTests
{
	private const int RampActionId = 110;
	private const string Task = "task";
	private const string InitialValue = "initial_value";
	private const string Speed = "speed";
	private const string StepDuration = "step_duration";

	[Fact]
	public void Recalculate_ChangeTask_RecomputesStepDuration()
	{
		var evaluator = BuildEvaluator();
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var action = registry.GetAction(RampActionId).Value;

		var step = BuildRampStep(
			task: 700f,
			initialValue: 500f,
			speed: 10f,
			stepDuration: 600f);

		var result = evaluator.Recalculate(step, action, Task, registry);

		result.IsSuccess.Should().BeTrue();
		var updated = result.Value;
		updated.Properties[new PropertyId(StepDuration)].AsFloat().Should().BeApproximately(1200f, 0.001f);
	}

	[Fact]
	public void Recalculate_ChangeSpeed_TargetFollowsRecalcOrderPriority()
	{
		var evaluator = BuildEvaluator();
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var action = registry.GetAction(RampActionId).Value;

		var step = BuildRampStep(task: 700f, initialValue: 500f, speed: 20f, stepDuration: 600f);

		var result = evaluator.Recalculate(step, action, Speed, registry);

		result.IsSuccess.Should().BeTrue();
		// recalc_order = [step_duration, speed, task, initial_value]
		// changed = speed -> target = step_duration
		var updated = result.Value;
		updated.Properties[new PropertyId(StepDuration)].AsFloat().Should().BeApproximately(600f, 0.001f);
	}

	[Fact]
	public void Recalculate_DivideByZero_ReturnsComputationFailedError()
	{
		var evaluator = BuildEvaluator();
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var action = registry.GetAction(RampActionId).Value;

		// speed=0 with changed=task -> target=step_duration -> (task-initial)/0 *60 = infinity
		var step = BuildRampStep(task: 800f, initialValue: 500f, speed: 0f, stepDuration: 600f);

		var result = evaluator.Recalculate(step, action, Task, registry);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainItemsAssignableTo<FormulaComputationFailedError>();
	}

	[Fact]
	public void Recalculate_NaNResult_ReturnsComputationFailedError()
	{
		// Build an action whose target expression produces NaN: 0/0
		var evaluator = BuildEvaluator();
		var registry = BuildNanRegistry();
		var action = registry.GetAction(999).Value;

		var step = new Step(
			999,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("a"), PropertyValue.FromFloat(0f))
				.Add(new PropertyId("b"), PropertyValue.FromFloat(0f)));

		var result = evaluator.Recalculate(step, action, "a", registry);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainItemsAssignableTo<FormulaComputationFailedError>();
	}

	[Fact]
	public void Recalculate_TargetOutOfRange_ReturnsTargetOutOfRangeError()
	{
		var evaluator = BuildEvaluator();
		// step_duration capped at 100 - massive task will overflow
		var registry = BuildRegistry(stepDurationMax: 100d, taskMax: 1000000d);
		var action = registry.GetAction(RampActionId).Value;

		var step = BuildRampStep(task: 100000f, initialValue: 500f, speed: 10f, stepDuration: 50f);

		var result = evaluator.Recalculate(step, action, Task, registry);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainItemsAssignableTo<FormulaTargetOutOfRangeError>();
	}

	[Theory]
	[InlineData(4.5d, 4)]
	[InlineData(3.5d, 4)]
	public void Recalculate_IntegerTarget_UsesBankersRounding(double targetSeed, int expected)
	{
		// Action with integer target: target = a + 0 (so we drive target via 'a').
		// recalc_order=[target,a]; changed=a -> target=target.
		var evaluator = BuildEvaluator();
		var registry = BuildIntegerTargetRegistry();
		var action = registry.GetAction(777).Value;

		var step = new Step(
			777,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("target"), PropertyValue.FromInt(0))
				.Add(new PropertyId("a"), PropertyValue.FromFloat((float)targetSeed)));

		var result = evaluator.Recalculate(step, action, "a", registry);

		result.IsSuccess.Should().BeTrue();
		result.Value.Properties[new PropertyId("target")].AsInt().Should().Be(expected);
	}

	[Fact]
	public void Recalculate_NullFormula_ThrowsInvalidOperationException()
	{
		var evaluator = BuildEvaluator();
		var registry = BuildRegistryWithoutFormula();
		var action = registry.GetAction(50).Value;
		var step = new Step(
			50,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("x"), PropertyValue.FromFloat(1f)));

		var act = () => evaluator.Recalculate(step, action, "x", registry);

		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void Recalculate_MissingRecalcOrderVariableInStep_ThrowsInvalidOperationException()
	{
		var evaluator = BuildEvaluator();
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var action = registry.GetAction(RampActionId).Value;

		// Build step missing the 'speed' property
		var step = new Step(
			RampActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(Task), PropertyValue.FromFloat(700f))
				.Add(new PropertyId(InitialValue), PropertyValue.FromFloat(500f))
				.Add(new PropertyId(StepDuration), PropertyValue.FromFloat(600f)));

		var act = () => evaluator.Recalculate(step, action, Task, registry);

		act.Should().Throw<InvalidOperationException>();
	}

	private static FormulaEvaluator BuildEvaluator()
	{
		return new FormulaEvaluator(NullLogger<FormulaEvaluator>.Instance);
	}

	private static Step BuildRampStep(float task, float initialValue, float speed, float stepDuration)
	{
		return new Step(
			RampActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(Task), PropertyValue.FromFloat(task))
				.Add(new PropertyId(InitialValue), PropertyValue.FromFloat(initialValue))
				.Add(new PropertyId(Speed), PropertyValue.FromFloat(speed))
				.Add(new PropertyId(StepDuration), PropertyValue.FromFloat(stepDuration)));
	}

	private static RecipeMetadataRegistry BuildRegistry(double stepDurationMax, double taskMax)
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["temp"] = new PropertyTypeDefinition("temp", "float", "decimal", "C", 0d, taskMax, null),
			["speed_t"] = new PropertyTypeDefinition("speed_t", "float", "decimal", "C/s", 0.001d, 1000d, null),
			["duration"] = new PropertyTypeDefinition("duration", "float", "decimal", "s", 0d, stepDurationMax, null)
		};

		var formula = BuildRampFormula();

		var actions = new Dictionary<int, ActionDefinition>
		{
			[RampActionId] = new ActionDefinition(
				Id: RampActionId,
				UiName: "t°C ramp",
				DeployDuration: DeployDuration.LongLasting,
				Properties: new[]
				{
					new ActionPropertyDefinition(Task, null, "temp", "0"),
					new ActionPropertyDefinition(InitialValue, null, "temp", "0"),
					new ActionPropertyDefinition(Speed, null, "speed_t", "1"),
					new ActionPropertyDefinition(StepDuration, null, "duration", "0")
				},
				Formula: formula)
		};

		return new RecipeMetadataRegistry(BuildAppConfiguration(properties, actions));
	}

	private static FormulaDefinition BuildRampFormula()
	{
		var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[StepDuration] = "(task - initial_value) / speed * 60",
			[Speed] = "(task - initial_value) / step_duration * 60",
			[Task] = "initial_value + speed * step_duration / 60",
			[InitialValue] = "task - speed * step_duration / 60"
		};

		var compiled = new Dictionary<string, NCalc.Domain.LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, src) in sources)
		{
			compiled[key] = FormulaIdentifierExtractor.ParseAndCompile(src).Value;
		}

		return new FormulaDefinition(
			recalcOrder: new[] { StepDuration, Speed, Task, InitialValue },
			expressionSources: sources,
			compiledExpressions: compiled);
	}

	private static RecipeMetadataRegistry BuildNanRegistry()
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["any"] = new PropertyTypeDefinition("any", "float", "decimal", null, -1e9, 1e9, null)
		};

		var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["a"] = "b / a",
			["b"] = "a / b"
		};

		var compiled = new Dictionary<string, NCalc.Domain.LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (k, s) in sources)
		{
			compiled[k] = FormulaIdentifierExtractor.ParseAndCompile(s).Value;
		}

		var formula = new FormulaDefinition(
			recalcOrder: new[] { "a", "b" },
			expressionSources: sources,
			compiledExpressions: compiled);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[999] = new ActionDefinition(
				Id: 999,
				UiName: "NaN action",
				DeployDuration: DeployDuration.Immediate,
				Properties: new[]
				{
					new ActionPropertyDefinition("a", null, "any", "0"),
					new ActionPropertyDefinition("b", null, "any", "0")
				},
				Formula: formula)
		};

		return new RecipeMetadataRegistry(BuildAppConfiguration(properties, actions));
	}

	private static RecipeMetadataRegistry BuildIntegerTargetRegistry()
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["intp"] = new PropertyTypeDefinition("intp", "int", "decimal", null, -1000d, 1000d, null),
			["floatp"] = new PropertyTypeDefinition("floatp", "float", "decimal", null, -1000d, 1000d, null)
		};

		var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["target"] = "a",
			["a"] = "target"
		};

		var compiled = new Dictionary<string, NCalc.Domain.LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (k, s) in sources)
		{
			compiled[k] = FormulaIdentifierExtractor.ParseAndCompile(s).Value;
		}

		var formula = new FormulaDefinition(
			recalcOrder: new[] { "target", "a" },
			expressionSources: sources,
			compiledExpressions: compiled);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[777] = new ActionDefinition(
				Id: 777,
				UiName: "int rounding",
				DeployDuration: DeployDuration.Immediate,
				Properties: new[]
				{
					new ActionPropertyDefinition("target", null, "intp", "0"),
					new ActionPropertyDefinition("a", null, "floatp", "0")
				},
				Formula: formula)
		};

		return new RecipeMetadataRegistry(BuildAppConfiguration(properties, actions));
	}

	private static RecipeMetadataRegistry BuildRegistryWithoutFormula()
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["any"] = new PropertyTypeDefinition("any", "float", "decimal", null, -1e9, 1e9, null)
		};

		var actions = new Dictionary<int, ActionDefinition>
		{
			[50] = new ActionDefinition(
				Id: 50,
				UiName: "plain",
				DeployDuration: DeployDuration.Immediate,
				Properties: new[] { new ActionPropertyDefinition("x", null, "any", "0") })
		};

		return new RecipeMetadataRegistry(BuildAppConfiguration(properties, actions));
	}

	private static AppConfiguration BuildAppConfiguration(
		Dictionary<string, PropertyTypeDefinition> properties,
		Dictionary<int, ActionDefinition> actions)
	{
		return new AppConfiguration(
			Properties: properties,
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default);
	}
}
