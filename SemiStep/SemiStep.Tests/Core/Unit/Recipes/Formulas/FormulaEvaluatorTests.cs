using System.Collections.Immutable;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NCalc.Domain;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Formulas;
using SemiStep.Core.Recipes.Formulas.Errors;
using SemiStep.Core.Recipes.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes.Formulas;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "Formulas")]
public sealed class FormulaEvaluatorTests
{
	private const int RampActionId = 110;
	private const string TaskColumnKey = "task";
	private const string InitialValue = "initial_value";
	private const string Speed = "speed";
	private const string StepDuration = "step_duration";

	[Fact]
	public void Recalculate_ChangeTask_RecomputesStepDuration()
	{
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(RampActionId).Value;

		var step = BuildRampStep(
			task: 700f,
			initialValue: 500f,
			speed: 10f,
			stepDuration: 600f);

		var result = evaluator.Recalculate(step, action, TaskColumnKey, ActiveColumnSetResolver.Resolve(action, step));

		result.IsSuccess.Should().BeTrue();
		var updated = result.Value;
		updated.Properties[new PropertyId(StepDuration)].AsFloat().Should().BeApproximately(1200f, 0.001f);
	}

	[Fact]
	public void Recalculate_ChangeSpeed_TargetFollowsRecalcOrderPriority()
	{
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(RampActionId).Value;

		var step = BuildRampStep(task: 700f, initialValue: 500f, speed: 20f, stepDuration: 600f);

		var result = evaluator.Recalculate(step, action, Speed, ActiveColumnSetResolver.Resolve(action, step));

		result.IsSuccess.Should().BeTrue();
		// recalc_order = [step_duration, speed, task, initial_value]
		// changed = speed -> target = step_duration
		var updated = result.Value;
		updated.Properties[new PropertyId(StepDuration)].AsFloat().Should().BeApproximately(600f, 0.001f);
	}

	[Fact]
	public void Recalculate_DivideByZero_ReturnsComputationFailedError()
	{
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(RampActionId).Value;

		// speed=0 with changed=task -> target=step_duration -> (task-initial)/0 *60 = infinity
		var step = BuildRampStep(task: 800f, initialValue: 500f, speed: 0f, stepDuration: 600f);

		var result = evaluator.Recalculate(step, action, TaskColumnKey, ActiveColumnSetResolver.Resolve(action, step));

		result.IsFailed.Should().BeTrue();
		var error = result.Errors.OfType<FormulaComputationFailedError>().FirstOrDefault();
		error.Should().NotBeNull();
		error!.Target.Should().Be(StepDuration);
	}

	[Fact]
	public void Recalculate_MixedCaseColumnKey_StillRecomputesTarget()
	{
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(RampActionId).Value;

		var step = BuildRampStep(task: 700f, initialValue: 500f, speed: 10f, stepDuration: 600f);

		var result = evaluator.Recalculate(step, action, "TASK", ActiveColumnSetResolver.Resolve(action, step));

		result.IsSuccess.Should().BeTrue();
		result.Value.Properties[new PropertyId(StepDuration)].AsFloat().Should().BeApproximately(1200f, 0.001f);
	}

	[Fact]
	public void Recalculate_ChangeSpeed_PreservesChangedColumn()
	{
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(RampActionId).Value;

		var step = BuildRampStep(task: 700f, initialValue: 500f, speed: 20f, stepDuration: 600f);

		var result = evaluator.Recalculate(step, action, Speed, ActiveColumnSetResolver.Resolve(action, step));

		result.IsSuccess.Should().BeTrue();
		result.Value.Properties[new PropertyId(Speed)].AsFloat().Should().BeApproximately(20f, 0.001f,
			"the user-edited column must remain unchanged after recalculation");
	}

	[Fact]
	public void Recalculate_TargetOutOfRange_ErrorCarriesTargetAndDescriptiveMessage()
	{
		var registry = BuildRegistry(stepDurationMax: 100d, taskMax: 1000000d);
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(RampActionId).Value;

		var step = BuildRampStep(task: 100000f, initialValue: 500f, speed: 10f, stepDuration: 50f);

		var result = evaluator.Recalculate(step, action, TaskColumnKey, ActiveColumnSetResolver.Resolve(action, step));

		result.IsFailed.Should().BeTrue();
		var error = result.Errors.OfType<FormulaComputationFailedError>().FirstOrDefault();
		error.Should().NotBeNull();
		error!.Target.Should().Be(StepDuration);
		error.Message.Should().Contain("100");
	}

	[Fact]
	public void Recalculate_NaNResult_ReturnsComputationFailedError()
	{
		// 0.0 / 0.0 is deterministically NaN under IEEE-754 double evaluation.
		var registry = BuildNanRegistry();
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(999).Value;

		var step = new Step(
			999,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("a"), PropertyValue.FromFloat(0f))
				.Add(new PropertyId("b"), PropertyValue.FromFloat(0f)));

		var result = evaluator.Recalculate(step, action, "a", ActiveColumnSetResolver.Resolve(action, step));

		result.IsFailed.Should().BeTrue();
		var error = result.Errors.OfType<FormulaComputationFailedError>().FirstOrDefault();
		error.Should().NotBeNull();
		error!.Target.Should().Be("b");
		error.Message.Should().Contain("NaN");
	}

	[Fact]
	public void Recalculate_InfinityResult_ReturnsComputationFailedError()
	{
		// 1.0 / 0.0 is deterministically +Infinity under IEEE-754 double evaluation.
		var registry = BuildNanRegistry();
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(999).Value;

		var step = new Step(
			999,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("a"), PropertyValue.FromFloat(1f))
				.Add(new PropertyId("b"), PropertyValue.FromFloat(0f)));

		var result = evaluator.Recalculate(step, action, "a", ActiveColumnSetResolver.Resolve(action, step));

		result.IsFailed.Should().BeTrue();
		var error = result.Errors.OfType<FormulaComputationFailedError>().FirstOrDefault();
		error.Should().NotBeNull();
		error!.Target.Should().Be("b");
		error.Message.Should().Contain("Infinity");
	}

	[Fact]
	public void Recalculate_Int32Overflow_ReturnsComputationFailedError()
	{
		var registry = BuildIntegerOverflowRegistry();
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(888).Value;

		var step = new Step(
			888,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("target"), PropertyValue.FromInt(0))
				.Add(new PropertyId("a"), PropertyValue.FromFloat(1e10f)));

		var result = evaluator.Recalculate(step, action, "a", ActiveColumnSetResolver.Resolve(action, step));

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainItemsAssignableTo<FormulaComputationFailedError>();
	}

	[Fact]
	public void Recalculate_NonNumericVariableInStep_ThrowsInvalidOperationException()
	{
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(RampActionId).Value;

		var step = new Step(
			RampActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(TaskColumnKey), PropertyValue.FromFloat(700f))
				.Add(new PropertyId(InitialValue), PropertyValue.FromFloat(500f))
				.Add(new PropertyId(Speed), PropertyValue.FromString("not a number"))
				.Add(new PropertyId(StepDuration), PropertyValue.FromFloat(600f)));

		var act = () => evaluator.Recalculate(step, action, TaskColumnKey, ActiveColumnSetResolver.Resolve(action, step));

		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void Recalculate_TargetOutOfRange_ReturnsComputationFailedError()
	{
		// step_duration capped at 100 - massive task will overflow
		var registry = BuildRegistry(stepDurationMax: 100d, taskMax: 1000000d);
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(RampActionId).Value;

		var step = BuildRampStep(task: 100000f, initialValue: 500f, speed: 10f, stepDuration: 50f);

		var result = evaluator.Recalculate(step, action, TaskColumnKey, ActiveColumnSetResolver.Resolve(action, step));

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainItemsAssignableTo<FormulaComputationFailedError>();
	}

	[Theory]
	[InlineData(4.5d, 4)]
	[InlineData(3.5d, 4)]
	public void Recalculate_IntegerTarget_UsesBankersRounding(double targetSeed, int expected)
	{
		// Action with integer target: target = a + 0 (so we drive target via 'a').
		// recalc_order=[target,a]; changed=a -> target=target.
		var registry = BuildIntegerTargetRegistry();
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(777).Value;

		var step = new Step(
			777,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("target"), PropertyValue.FromInt(0))
				.Add(new PropertyId("a"), PropertyValue.FromFloat((float)targetSeed)));

		var result = evaluator.Recalculate(step, action, "a", ActiveColumnSetResolver.Resolve(action, step));

		result.IsSuccess.Should().BeTrue();
		result.Value.Properties[new PropertyId("target")].AsInt().Should().Be(expected);
	}

	[Fact]
	public void Recalculate_GroupValidationFailure_ReturnsFormulaComputationFailedError()
	{
		var registry = BuildGroupedTargetRegistry();
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(555).Value;

		// target=int via group "g1" (members 1,2). a=99 -> target=99 -> not in group.
		var step = new Step(
			555,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("target"), PropertyValue.FromInt(1))
				.Add(new PropertyId("a"), PropertyValue.FromInt(99)));

		var result = evaluator.Recalculate(step, action, "a", ActiveColumnSetResolver.Resolve(action, step));

		result.IsFailed.Should().BeTrue();
		var error = result.Errors.OfType<FormulaComputationFailedError>().FirstOrDefault();
		error.Should().NotBeNull();
		error!.Target.Should().Be("target");
	}

	[Fact]
	public void Recalculate_NullFormula_ThrowsInvalidOperationException()
	{
		var registry = BuildRegistryWithoutFormula();
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(50).Value;
		var step = new Step(
			50,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("x"), PropertyValue.FromFloat(1f)));

		var act = () => evaluator.Recalculate(step, action, "x", ActiveColumnSetResolver.Resolve(action, step));

		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void Recalculate_MissingRecalcOrderVariableInStep_ThrowsInvalidOperationException()
	{
		var registry = BuildRegistry(stepDurationMax: 100000d, taskMax: 10000d);
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(RampActionId).Value;

		// Build step missing the 'speed' property
		var step = new Step(
			RampActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(TaskColumnKey), PropertyValue.FromFloat(700f))
				.Add(new PropertyId(InitialValue), PropertyValue.FromFloat(500f))
				.Add(new PropertyId(StepDuration), PropertyValue.FromFloat(600f)));

		var act = () => evaluator.Recalculate(step, action, TaskColumnKey, ActiveColumnSetResolver.Resolve(action, step));

		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void Recalculate_InactiveRecalcVariable_SkipsAndLeavesStepUnchanged()
	{
		// The 'b' column is active only when selector 'mode' == 2. With mode == 1 it is inactive,
		// so the recalc references a column that is not in the active set and must be skipped.
		var registry = BuildSelectorGatedRegistry();
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(444).Value;

		var step = new Step(
			444,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("mode"), PropertyValue.FromInt(1))
				.Add(new PropertyId("a"), PropertyValue.FromFloat(5f)));

		var activeColumns = ActiveColumnSetResolver.Resolve(action, step);
		activeColumns.Should().NotContain("b");

		var result = evaluator.Recalculate(step, action, "a", activeColumns);

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().BeSameAs(step, "an inactive recalc variable must skip the recalc and leave the step untouched");
	}

	[Fact]
	public void Recalculate_AllRecalcVariablesActive_RecomputesAsBefore()
	{
		// Same registry, but mode == 2 makes 'b' active, so the formula recalculates normally:
		// changed = a -> target = b -> b = a * 2.
		var registry = BuildSelectorGatedRegistry();
		var evaluator = BuildEvaluator(registry);
		var action = registry.GetAction(444).Value;

		var step = new Step(
			444,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("mode"), PropertyValue.FromInt(2))
				.Add(new PropertyId("a"), PropertyValue.FromFloat(5f))
				.Add(new PropertyId("b"), PropertyValue.FromFloat(0f)));

		var activeColumns = ActiveColumnSetResolver.Resolve(action, step);
		activeColumns.Should().Contain("b");

		var result = evaluator.Recalculate(step, action, "a", activeColumns);

		result.IsSuccess.Should().BeTrue();
		result.Value.Properties[new PropertyId("b")].AsFloat().Should().BeApproximately(10f, 0.001f);
	}

	private static RecipeMetadataRegistry BuildSelectorGatedRegistry()
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["intp"] = new PropertyTypeDefinition("intp", "int", "decimal", null, -1000d, 1000d, null),
			["floatp"] = new PropertyTypeDefinition("floatp", "float", "decimal", null, -1000d, 1000d, null)
		};

		var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["a"] = "b / 2",
			["b"] = "a * 2"
		};

		var compiled = new Dictionary<string, LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, source) in sources)
		{
			compiled[key] = FormulaIdentifierExtractor.Parse(source).Value.LogicalExpression;
		}

		var formula = new FormulaDefinition(
			recalcOrder: new[] { "a", "b" },
			compiledExpressions: compiled);

		// The activation condition on 'b' is produced by the resolver from the selector's targets,
		// not set directly: 'mode' targets subaction 4444 for value 2, which contributes 'b'.
		// The resolved union for action 444 is [mode, b, a] with 'b' active iff mode == 2.
		var actions = new Dictionary<int, ActionDefinition>
		{
			[444] = new ActionDefinition(
				id: 444,
				uiName: "selector gated",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition(
						"mode",
						null,
						"intp",
						"1",
						Targets: new Dictionary<int, int> { [2] = 4444 }),
					new ActionPropertyDefinition("a", null, "floatp", "0")
				},
				formula: formula),
			[4444] = new ActionDefinition(
				id: 4444,
				uiName: "manual branch",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition("b", null, "floatp", "0")
				},
				role: ActionRole.Subaction)
		};

		return new RecipeMetadataRegistry(BuildAppConfiguration(properties, actions));
	}

	private static FormulaEvaluator BuildEvaluator(RecipeMetadataRegistry registry)
	{
		return new FormulaEvaluator(registry, NullLogger<FormulaEvaluator>.Instance);
	}

	private static Step BuildRampStep(float task, float initialValue, float speed, float stepDuration)
	{
		return new Step(
			RampActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(TaskColumnKey), PropertyValue.FromFloat(task))
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
				id: RampActionId,
				uiName: "t°C ramp",
				deployDuration: DeployDuration.LongLasting,
				properties: new[]
				{
					new ActionPropertyDefinition(TaskColumnKey, null, "temp", "0"),
					new ActionPropertyDefinition(InitialValue, null, "temp", "0"),
					new ActionPropertyDefinition(Speed, null, "speed_t", "1"),
					new ActionPropertyDefinition(StepDuration, null, "duration", "0")
				},
				formula: formula)
		};

		return new RecipeMetadataRegistry(BuildAppConfiguration(properties, actions));
	}

	private static FormulaDefinition BuildRampFormula()
	{
		var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[StepDuration] = "(task - initial_value) / speed * 60",
			[Speed] = "(task - initial_value) / step_duration * 60",
			[TaskColumnKey] = "initial_value + speed * step_duration / 60",
			[InitialValue] = "task - speed * step_duration / 60"
		};

		var compiled = new Dictionary<string, LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, src) in sources)
		{
			compiled[key] = FormulaIdentifierExtractor.Parse(src).Value.LogicalExpression;
		}

		return new FormulaDefinition(
			recalcOrder: new[] { StepDuration, Speed, TaskColumnKey, InitialValue },
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

		var compiled = new Dictionary<string, LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (k, s) in sources)
		{
			compiled[k] = FormulaIdentifierExtractor.Parse(s).Value.LogicalExpression;
		}

		var formula = new FormulaDefinition(
			recalcOrder: new[] { "a", "b" },
			compiledExpressions: compiled);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[999] = new ActionDefinition(
				id: 999,
				uiName: "NaN action",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition("a", null, "any", "0"),
					new ActionPropertyDefinition("b", null, "any", "0")
				},
				formula: formula)
		};

		return new RecipeMetadataRegistry(BuildAppConfiguration(properties, actions));
	}

	private static RecipeMetadataRegistry BuildIntegerOverflowRegistry()
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["intp"] = new PropertyTypeDefinition("intp", "int", "decimal", null, null, null, null),
			["floatp"] = new PropertyTypeDefinition("floatp", "float", "decimal", null, null, null, null)
		};

		var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["target"] = "a * 1000000000",
			["a"] = "target"
		};

		var compiled = new Dictionary<string, LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (k, s) in sources)
		{
			compiled[k] = FormulaIdentifierExtractor.Parse(s).Value.LogicalExpression;
		}

		var formula = new FormulaDefinition(
			recalcOrder: new[] { "target", "a" },
			compiledExpressions: compiled);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[888] = new ActionDefinition(
				id: 888,
				uiName: "int overflow",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition("target", null, "intp", "0"),
					new ActionPropertyDefinition("a", null, "floatp", "0")
				},
				formula: formula)
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

		var compiled = new Dictionary<string, LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (k, s) in sources)
		{
			compiled[k] = FormulaIdentifierExtractor.Parse(s).Value.LogicalExpression;
		}

		var formula = new FormulaDefinition(
			recalcOrder: new[] { "target", "a" },
			compiledExpressions: compiled);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[777] = new ActionDefinition(
				id: 777,
				uiName: "int rounding",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition("target", null, "intp", "0"),
					new ActionPropertyDefinition("a", null, "floatp", "0")
				},
				formula: formula)
		};

		return new RecipeMetadataRegistry(BuildAppConfiguration(properties, actions));
	}

	private static RecipeMetadataRegistry BuildGroupedTargetRegistry()
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["intp"] = new PropertyTypeDefinition("intp", "int", "decimal", null, -1000d, 1000d, null)
		};
		EnsureStringSentinelProperty(properties);

		var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["target"] = "a",
			["a"] = "target"
		};

		var compiled = new Dictionary<string, LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (k, s) in sources)
		{
			compiled[k] = FormulaIdentifierExtractor.Parse(s).Value.LogicalExpression;
		}

		var formula = new FormulaDefinition(
			recalcOrder: new[] { "target", "a" },
			compiledExpressions: compiled);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[555] = new ActionDefinition(
				id: 555,
				uiName: "grouped target",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition("target", "g1", "intp", "1"),
					new ActionPropertyDefinition("a", null, "intp", "1")
				},
				formula: formula)
		};

		var groups = new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["g1"] = new GroupDefinition("g1", new Dictionary<int, string>
			{
				[1] = "one",
				[2] = "two"
			})
		};

		return new RecipeMetadataRegistry(new AppConfiguration(
			Properties: properties,
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: groups,
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: AppUiOptions.Default));
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
				id: 50,
				uiName: "plain",
				deployDuration: DeployDuration.Immediate,
				properties: new[] { new ActionPropertyDefinition("x", null, "any", "0") })
		};

		return new RecipeMetadataRegistry(BuildAppConfiguration(properties, actions));
	}

	private static AppConfiguration BuildAppConfiguration(
		Dictionary<string, PropertyTypeDefinition> properties,
		Dictionary<int, ActionDefinition> actions)
	{
		EnsureStringSentinelProperty(properties);

		return new AppConfiguration(
			Properties: properties,
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: AppUiOptions.Default);
	}

	private static void EnsureStringSentinelProperty(Dictionary<string, PropertyTypeDefinition> properties)
	{
		if (properties.Values.Any(property =>
			string.Equals(property.SystemType, "string", StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}

		properties["comment"] = new PropertyTypeDefinition(
			Id: "comment",
			SystemType: "string",
			FormatKind: "text",
			Units: null,
			Min: null,
			Max: null,
			MaxLength: 32);
	}
}
