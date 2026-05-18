using System.Collections.Immutable;
using System.Globalization;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Formulas;
using SemiStep.Core.Recipes.Formulas.Errors;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes;

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

	[Fact]
	public void UpdateStepProperty_FormulaAction_RecalculatesCoupledCellAndGrowsUndoByOne()
	{
		var harness = BuildHarness();
		harness.SeedRampStep(task: 700f, initialValue: 500f, speed: 10f, stepDuration: 600f);

		var undoBefore = CountUndo(harness.Session);

		var result = harness.Session.UpdateStepProperty(0, Task, "900");

		result.IsSuccess.Should().BeTrue();
		var step = harness.Session.Current.Steps[0];
		// (900 - 500) / 10 * 60 = 2400
		step.Properties[new PropertyId(StepDuration)].AsFloat().Should().BeApproximately(2400f, 0.001f);

		CountUndo(harness.Session).Should().Be(undoBefore + 1, "one user edit + recalc collapses into one undo unit");
	}

	[Fact]
	public void UpdateStepProperty_DivideByZero_RejectsEditAndRecipeUnchanged()
	{
		var harness = BuildHarness();
		harness.SeedRampStep(task: 700f, initialValue: 500f, speed: 0f, stepDuration: 600f);

		var stepBefore = harness.Session.Current.Steps[0];
		var undoBefore = CountUndo(harness.Session);

		var result = harness.Session.UpdateStepProperty(0, Task, "800");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainItemsAssignableTo<FormulaComputationFailedError>();

		harness.Session.Current.Steps[0].Should().Be(stepBefore, "rejected edit must not mutate the recipe");
		CountUndo(harness.Session).Should().Be(undoBefore, "rejected edit must not push history");
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

	private static int CountUndo(RecipeSession session)
	{
		// _undoStack is private — derive a count via CanUndo + a probing Undo() pattern is destructive.
		// Use reflection on the field to avoid mutating state.
		var field = typeof(RecipeSession).GetField("_undoStack",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		var list = (System.Collections.IList)field!.GetValue(session)!;
		return list.Count;
	}

	private static Harness BuildHarness()
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["temp"] = new PropertyTypeDefinition("temp", "float", "decimal", "C", 0d, 1_000_000d, null),
			["speed_t"] = new PropertyTypeDefinition("speed_t", "float", "decimal", "C/s", 0d, 1000d, null),
			["duration"] = new PropertyTypeDefinition("duration", "float", "decimal", "s", 0d, 1_000_000d, null),
			["plain"] = new PropertyTypeDefinition("plain", "float", "decimal", null, -1e9, 1e9, null)
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
				Formula: formula),
			[PlainActionId] = new ActionDefinition(
				Id: PlainActionId,
				UiName: "Plain",
				DeployDuration: DeployDuration.Immediate,
				Properties: new[]
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
			PlcConfiguration: PlcConfiguration.Default);

		var registry = new RecipeMetadataRegistry(configuration);
		var analyzer = new RecipeAnalyzer(registry);
		var evaluator = new FormulaEvaluator(NullLogger<FormulaEvaluator>.Instance);
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
					.Add(new PropertyId(StepDuration), PropertyValue.FromFloat(stepDuration)));

			var recipe = Session.Current.AppendStep(step);
			Session.Apply(recipe).IsSuccess.Should().BeTrue();
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
