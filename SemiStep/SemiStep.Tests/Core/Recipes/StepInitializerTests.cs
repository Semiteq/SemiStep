using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Recipes;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "NestedActions")]
public sealed class StepInitializerTests
{
	private const int RootId = 900;
	private const int IntermediateSubactionId = 901;
	private const int LeafSubactionId = 902;
	private const int SiblingSubactionId = 903;

	// The intermediate selector's enabling value: choosing it pulls in the leaf subaction.
	private const int IntermediateEnabling = 1;

	// The leaf selector's enabling value: choosing it pulls in the depth-2 leaf column.
	private const int LeafEnabling = 1;

	[Fact]
	public void Create_Depth2Chain_IntermediateSelectorDefaultsToEnabling_SeedsLeafColumn()
	{
		// The depth-1 selector defaults to its enabling value, which activates the intermediate
		// selector; the intermediate selector ALSO defaults to its enabling value, which activates
		// the depth-2 leaf column. A single-pass seeding computes the active set once from the
		// always-active columns only, so the leaf column is never reached: the intermediate
		// selector is not yet seeded when the active set is computed. The fixpoint seeding must
		// reach the leaf column.
		var registry = BuildRegistry();
		var action = registry.GetAction(RootId).Value;

		var step = StepInitializer.Create(action, registry);

		step.Properties.Should().ContainKey(new PropertyId("root_sel"));
		step.Properties.Should().ContainKey(new PropertyId("leaf_sel"));
		step.Properties[new PropertyId("leaf_value")].AsFloat().Should().Be(50f);
	}

	[Fact]
	public void Create_Depth2Chain_InactiveSiblingBranch_IsNotSeeded()
	{
		// The sibling branch activates only when the depth-1 selector holds value 2; it defaults to
		// value 1, so the sibling column never activates and its slot must be left unseeded
		// (serialises as 0 rather than a stale default).
		var registry = BuildRegistry();
		var action = registry.GetAction(RootId).Value;

		var step = StepInitializer.Create(action, registry);

		step.Properties.Should().NotContainKey(new PropertyId("sibling_value"));
	}

	private static RecipeMetadataRegistry BuildRegistry()
	{
		var properties = new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateInt("enum"),
			TestPropertyTypeDefinitionBuilder.CreateFloat("percent"),
			TestPropertyTypeDefinitionBuilder.CreateString("comment", TestRecipeMetadataRegistryFactory.DefaultStringMaxLength)
		};

		// leaf subaction: a single percent column, pulled in by the intermediate selector.
		var leaf = Subaction(LeafSubactionId, Percent("leaf_value"));

		// intermediate subaction: itself carries a selector that defaults to its enabling value,
		// so the leaf column should be active at creation.
		var intermediate = Subaction(
			IntermediateSubactionId,
			SelectorWithDefault("leaf_sel", LeafEnabling, (LeafEnabling, LeafSubactionId)));

		// sibling subaction: a percent column reached only when the root selector holds value 2.
		var sibling = Subaction(SiblingSubactionId, Percent("sibling_value"));

		// root: the depth-1 selector defaults to its enabling value (1 -> intermediate), so the
		// whole chain should seed; value 2 would pull in the sibling branch instead.
		var root = new ActionDefinition(
			id: RootId,
			uiName: "Depth2Root",
			deployDuration: DeployDuration.LongLasting,
			properties: new List<ActionPropertyDefinition>
			{
				SelectorWithDefault(
					"root_sel",
					IntermediateEnabling,
					(IntermediateEnabling, IntermediateSubactionId),
					(2, SiblingSubactionId)),
				Column("comment", "comment")
			},
			role: ActionRole.Action);

		return TestRecipeMetadataRegistryFactory.Build(
			properties,
			actions: new Dictionary<int, ActionDefinition>
			{
				[RootId] = root,
				[IntermediateSubactionId] = intermediate,
				[LeafSubactionId] = leaf,
				[SiblingSubactionId] = sibling
			});
	}

	private static ActionDefinition Subaction(int id, params ActionPropertyDefinition[] columns)
	{
		return new ActionDefinition(
			id,
			$"sub-{id}",
			DeployDuration.LongLasting,
			columns,
			null,
			ActionRole.Subaction);
	}

	private static ActionPropertyDefinition Column(string key, string propertyTypeId)
	{
		return new ActionPropertyDefinition(key, null, propertyTypeId, null);
	}

	private static ActionPropertyDefinition Percent(string key)
	{
		return new ActionPropertyDefinition(key, null, "percent", "50");
	}

	private static ActionPropertyDefinition SelectorWithDefault(
		string key,
		int defaultValue,
		params (int Value, int TargetId)[] targets)
	{
		var map = targets.ToDictionary(t => t.Value, t => t.TargetId);
		return new ActionPropertyDefinition(
			Key: key,
			GroupName: null,
			PropertyTypeId: "enum",
			DefaultValue: defaultValue.ToString(),
			Targets: map);
	}
}
