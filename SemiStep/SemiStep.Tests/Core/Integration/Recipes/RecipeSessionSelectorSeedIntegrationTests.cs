using System.Collections.Immutable;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NCalc.Domain;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Formulas;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Recipes;

/// <summary>
/// Integration coverage for the selector-edit seed path through <see cref="RecipeSession"/>:
/// a newly-activated subaction column with no <c>default_value</c> must be seeded with its
/// resolved default (zero for a numeric column) rather than an empty string that would fail
/// validation and reject the whole selector edit.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "NestedActions")]
public sealed class RecipeSessionSelectorSeedIntegrationTests
{
	private const int BranchingActionId = 300;
	private const string SelectorColumn = "branch_sel";
	private const string SubValueColumn = "sub_value";
	private const string SubNoDefaultColumn = "sub_nodefault";
	private const int ManualValue = 1;

	[Fact]
	public async Task SelectorEdit_SeedsActivatedColumnWithNoDefault_AsZero_AndSucceeds()
	{
		var session = await BuildSessionAsync();

		session.AppendStep(BranchingActionId).IsSuccess.Should().BeTrue();

		// sub_value (default "50") and sub_nodefault (no default) are both inactive under Авто.
		var step = session.Current.Steps[0];
		step.Properties.ContainsKey(new PropertyId(SubNoDefaultColumn)).Should().BeFalse();

		var result = session.UpdateStepForSelectorChange(
			0,
			SelectorColumn,
			ManualValue.ToString(),
			columnsToDrop: System.Array.Empty<string>(),
			columnsToSeed: new[] { SubValueColumn, SubNoDefaultColumn });

		result.IsSuccess.Should().BeTrue(
			"a column with no default_value must seed its resolved zero default, not an empty string");

		var updated = session.Current.Steps[0];
		updated.Properties[new PropertyId(SubValueColumn)].AsFloat().Should().Be(50f);
		updated.Properties[new PropertyId(SubNoDefaultColumn)].AsFloat().Should().Be(0f);
	}

	[Fact]
	[Trait("Area", "Formulas")]
	public void SelectorChange_DeactivatingRecalcVariable_SucceedsWithoutRecalcOrThrow()
	{
		// Action 444 has a formula (recalc order a -> b) where 'b' is active only when selector
		// 'mode' == 2. Switching 'mode' to 1 deactivates 'b' and drops it. The shared
		// TryApplyFormulaRecalc path must skip the recalc (instead of throwing on the missing
		// inactive variable) and the edit must succeed in one undo unit.
		var registry = BuildSelectorGatedRegistry();
		var session = BuildSession(registry);

		var step = new Step(
			444,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("mode"), PropertyValue.FromInt(2))
				.Add(new PropertyId("a"), PropertyValue.FromFloat(5f))
				.Add(new PropertyId("b"), PropertyValue.FromFloat(10f)));
		session.LoadAsCurrent(Recipe.Empty.AppendStep(step)).IsSuccess.Should().BeTrue();

		var result = session.UpdateStepForSelectorChange(
			0,
			"mode",
			"1",
			columnsToDrop: new[] { "b" },
			columnsToSeed: System.Array.Empty<string>());

		result.IsSuccess.Should().BeTrue("deactivating a recalc variable must skip recalc, not throw");

		var updated = session.Current.Steps[0];
		updated.Properties[new PropertyId("mode")].AsInt().Should().Be(1);
		updated.Properties.ContainsKey(new PropertyId("b")).Should().BeFalse("the deactivated column is dropped");
		updated.Properties[new PropertyId("a")].AsFloat().Should().Be(5f, "the untouched active column is unchanged");
	}

	private static RecipeMetadataRegistry BuildSelectorGatedRegistry()
	{
		var properties = new Dictionary<string, PropertyTypeDefinition>
		{
			["intp"] = new PropertyTypeDefinition("intp", "int", "numeric", null, -1e9, 1e9, null),
			["floatp"] = new PropertyTypeDefinition("floatp", "float", "decimal", null, -1e9, 1e9, null),
			["string"] = new PropertyTypeDefinition("string", "string", "text", null, null, null, 64)
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

		var configuration = new AppConfiguration(
			Properties: properties,
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: AppUiOptions.Default);

		return new RecipeMetadataRegistry(configuration);
	}

	private static RecipeSession BuildSession(RecipeMetadataRegistry registry)
	{
		var analyzer = new RecipeAnalyzer(registry);
		var evaluator = new FormulaEvaluator(registry, NullLogger<FormulaEvaluator>.Instance);
		var sync = new StubPlcSyncService();

		return new RecipeSession(
			analyzer,
			registry,
			evaluator,
			sync,
			NullLogger<RecipeSession>.Instance);
	}

	private static async Task<RecipeSession> BuildSessionAsync()
	{
		var configResult = await ConfigTestHelper.LoadStandaloneCaseAsync("NestedActionsValid");
		configResult.IsSuccess.Should().BeTrue(configResult.IsFailed
			? string.Join("; ", configResult.Errors.Select(error => error.Message))
			: string.Empty);

		var registry = new RecipeMetadataRegistry(configResult.Value);
		return BuildSession(registry);
	}
}
