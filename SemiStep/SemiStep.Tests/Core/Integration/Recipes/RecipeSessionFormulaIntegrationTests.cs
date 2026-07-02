using System.Collections.Immutable;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NCalc;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Formulas;
using SemiStep.Core.Recipes.Formulas.Errors;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Recipes;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Formulas")]
public sealed class RecipeSessionFormulaIntegrationTests
{
	private const int RampActionId = 110;
	private const int PlainActionId = 90;
	private const string Task = "task";
	private const string InitialValue = "initial_value";
	private const string Speed = "speed";
	private const string StepDuration = "step_duration";
	private const string Comment = "comment";

	[Fact]
	public void UpdateStepProperty_FormulaAction_RecalculatesCoupledCellAndGrowsUndoByOne()
	{
		var harness = BuildHarness();
		harness.SeedConsistentRampStep();

		var undoBefore = harness.Session.UndoCount;

		var result = harness.Session.UpdateStepProperty(0, Task, "900");

		result.IsSuccess.Should().BeTrue();
		var step = harness.Session.Current.Steps[0];
		// (900 - 500) / 10 * 60 = 2400
		step.Properties[new PropertyId(StepDuration)].AsFloat().Should().BeApproximately(2400f, 0.001f);

		harness.Session.UndoCount.Should().Be(undoBefore + 1,
			"one user edit + recalc collapses into one undo unit");
	}

	[Fact]
	public void UpdateStepProperty_DivideByZero_RejectsEditAndRecipeUnchanged()
	{
		var harness = BuildHarness();
		harness.SeedRampStep(task: 700f, initialValue: 500f, speed: 0f, stepDuration: 600f);

		var stepBefore = harness.Session.Current.Steps[0];
		var undoBefore = harness.Session.UndoCount;

		var result = harness.Session.UpdateStepProperty(0, Task, "800");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainItemsAssignableTo<FormulaComputationFailedError>();

		harness.Session.Current.Steps[0].Should().Be(stepBefore, "rejected edit must not mutate the recipe");
		harness.Session.UndoCount.Should().Be(undoBefore, "rejected edit must not push history");
	}

	[Fact]
	public void UpdateStepProperty_DivideByZero_ReasonPropagatesAsFormulaComputationFailedError()
	{
		var harness = BuildHarness();
		harness.SeedRampStep(task: 700f, initialValue: 500f, speed: 0f, stepDuration: 600f);

		var result = harness.Session.UpdateStepProperty(0, Task, "800");

		result.IsFailed.Should().BeTrue();
		var formulaError = result.Errors.OfType<FormulaComputationFailedError>().FirstOrDefault();
		formulaError.Should().NotBeNull();
		formulaError!.Target.Should().Be(StepDuration);
	}

	[Fact]
	public void UpdateStepProperty_ActionWithoutFormula_UpdatesCellNormally()
	{
		var harness = BuildHarness();
		harness.SeedPlainStep(value: 5f);

		var result = harness.Session.UpdateStepProperty(0, Task, "42");

		result.IsSuccess.Should().BeTrue();
		harness.Session.Current.Steps[0].Properties[new PropertyId(Task)].AsFloat()
			.Should().BeApproximately(42f, 0.001f);
	}

	[Fact]
	public void UpdateStepProperty_OutOfRangeRecalcTarget_RejectsAndPropagatesError()
	{
		// step_duration capped at 100 — the recalc must overflow.
		var harness = BuildHarness(stepDurationMax: 100d);
		harness.SeedRampStep(task: 100f, initialValue: 0f, speed: 10f, stepDuration: 60f);

		var stepBefore = harness.Session.Current.Steps[0];
		var undoBefore = harness.Session.UndoCount;

		var result = harness.Session.UpdateStepProperty(0, Task, "10000");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainItemsAssignableTo<FormulaComputationFailedError>();
		harness.Session.Current.Steps[0].Should().Be(stepBefore);
		harness.Session.UndoCount.Should().Be(undoBefore);
	}

	[Fact]
	public void UpdateStepProperty_NonFormulaColumnOnFormulaAction_BypassesEvaluator()
	{
		// Editing `comment` (string column) on an action with a formula must not trigger
		// the formula evaluator and must not error, even if other cells form an inconsistent state.
		var harness = BuildHarness();
		harness.SeedRampStep(task: 700f, initialValue: 500f, speed: 0f, stepDuration: 600f);

		var result = harness.Session.UpdateStepProperty(0, Comment, "hello");

		result.IsSuccess.Should().BeTrue();
		harness.Session.Current.Steps[0].Properties[new PropertyId(Comment)].AsString()
			.Should().Be("hello");
	}

	[Fact]
	public void UpdateStepProperty_InconsistentSeed_RecalculatesToConsistentState()
	{
		// Seed deliberately violates the formula (step_duration says 600 but should be 1200).
		// After editing task to 900, step_duration must become 2400 — i.e. evaluator recomputes
		// from the NEW value, not from the pre-existing inconsistent baseline.
		var harness = BuildHarness();
		harness.SeedRampStep(task: 700f, initialValue: 500f, speed: 10f, stepDuration: 600f);

		var result = harness.Session.UpdateStepProperty(0, Task, "900");

		result.IsSuccess.Should().BeTrue();
		harness.Session.Current.Steps[0].Properties[new PropertyId(StepDuration)].AsFloat()
			.Should().BeApproximately(2400f, 0.001f);
	}

	private static Harness BuildHarness(double stepDurationMax = 1_000_000d)
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["temp"] = new PropertyTypeDefinition("temp", "float", "decimal", "C", 0d, 1_000_000d, null),
			["speed_t"] = new PropertyTypeDefinition("speed_t", "float", "decimal", "C/s", 0d, 1000d, null),
			["duration"] = new PropertyTypeDefinition("duration", "float", "decimal", "s", 0d, stepDurationMax, null),
			["plain"] = new PropertyTypeDefinition("plain", "float", "decimal", null, -1e9, 1e9, null),
			["text"] = new PropertyTypeDefinition("text", "string", "decimal", null, null, null, 100)
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
					new ActionPropertyDefinition(Task, null, "temp", "0"),
					new ActionPropertyDefinition(InitialValue, null, "temp", "0"),
					new ActionPropertyDefinition(Speed, null, "speed_t", "1"),
					new ActionPropertyDefinition(StepDuration, null, "duration", "0"),
					new ActionPropertyDefinition(Comment, null, "text", "")
				},
				formula: formula),
			[PlainActionId] = new ActionDefinition(
				id: PlainActionId,
				uiName: "Plain",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition(Task, null, "plain", "0")
				})
		};

		var configuration = new AppConfiguration(
			Properties: properties,
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: AppUiOptions.Default);

		var registry = new RecipeMetadataRegistry(configuration);
		var analyzer = new RecipeAnalyzer(registry);
		var evaluator = new FormulaEvaluator(registry, NullLogger<FormulaEvaluator>.Instance);
		var sync = new StubPlcSyncService();

		var session = new RecipeSession(
			analyzer,
			registry,
			evaluator,
			sync,
			NullLogger<RecipeSession>.Instance);

		return new Harness(session);
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

		var compiled = new Dictionary<string, LogicalExpression>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, src) in sources)
		{
			compiled[key] = FormulaIdentifierExtractor.Parse(src).Value.LogicalExpression;
		}

		return new FormulaDefinition(
			recalcOrder: new[] { StepDuration, Speed, Task, InitialValue },
			compiledExpressions: compiled);
	}

	private sealed class Harness
	{
		public RecipeSession Session { get; }

		public Harness(RecipeSession session)
		{
			Session = session;
		}

		public void SeedRampStep(float task, float initialValue, float speed, float stepDuration)
		{
			var step = new Step(
				RampActionId,
				ImmutableDictionary<PropertyId, PropertyValue>.Empty
					.Add(new PropertyId(Task), PropertyValue.FromFloat(task))
					.Add(new PropertyId(InitialValue), PropertyValue.FromFloat(initialValue))
					.Add(new PropertyId(Speed), PropertyValue.FromFloat(speed))
					.Add(new PropertyId(StepDuration), PropertyValue.FromFloat(stepDuration))
					.Add(new PropertyId(Comment), PropertyValue.FromString("")));

			var recipe = Session.Current.AppendStep(step);
			Session.Apply(recipe).IsSuccess.Should().BeTrue();
		}

		public void SeedConsistentRampStep()
		{
			// Consistent baseline: (700 - 500) / 10 * 60 = 1200
			SeedRampStep(task: 700f, initialValue: 500f, speed: 10f, stepDuration: 1200f);
		}

		public void SeedPlainStep(float value)
		{
			var step = new Step(
				PlainActionId,
				ImmutableDictionary<PropertyId, PropertyValue>.Empty
					.Add(new PropertyId(Task), PropertyValue.FromFloat(value)));

			var recipe = Session.Current.AppendStep(step);
			Session.Apply(recipe).IsSuccess.Should().BeTrue();
		}
	}
}
