using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "RecipeMetadataRegistry")]
public sealed class RecipeMetadataRegistryTests
{
	[Fact]
	public void GetAction_PrimaryProperties_EqualResolverUnion()
	{
		var registry = BuildNestedRegistry();

		var action = registry.GetAction(300);

		action.IsSuccess.Should().BeTrue();
		action.Value.Properties.Select(property => property.Key).Should().Equal(
			"icp_power", "icp_match", "icp_load", "icp_tune");
	}

	[Fact]
	public void TryGetAction_KnownId_ReturnsTrueAndAction()
	{
		var registry = BuildNestedRegistry();

		var found = registry.TryGetAction(300, out var action);

		found.Should().BeTrue();
		action.Should().NotBeNull();
		action!.Id.Should().Be(300);
		action.Should().BeSameAs(registry.GetAction(300).Value);
	}

	[Fact]
	public void TryGetAction_UnknownId_ReturnsFalseAndDefault_WithoutThrowing()
	{
		var registry = BuildNestedRegistry();

		var found = registry.TryGetAction(999, out var action);

		found.Should().BeFalse();
		action.Should().BeNull();
	}

	[Fact]
	public void Subaction_DoesNotEnterRuntimeActionCollections()
	{
		var registry = BuildNestedRegistry();

		registry.ActionExists(3002).IsFailed.Should().BeTrue();
		registry.GetAllActions().Should().OnlyContain(action => action.Role == ActionRole.Action);
	}

	private static RecipeMetadataRegistry BuildNestedRegistry()
	{
		var subaction = new ActionDefinition(
			3002,
			"ICP manual",
			DeployDuration.LongLasting,
			[Column("icp_load", "percent"), Column("icp_tune", "percent")],
			null,
			ActionRole.Subaction);

		var root = new ActionDefinition(
			300,
			"Etch",
			DeployDuration.LongLasting,
			[Column("icp_power", "power"), Selector("icp_match", "enum", 2, 3002)],
			null,
			ActionRole.Action);

		return BuildRegistryWith(new Dictionary<int, ActionDefinition>
		{
			[300] = root,
			[3002] = subaction
		});
	}

	[Fact]
	[Trait("Area", "NestedActions")]
	public void Constructor_CyclicReferenceGraph_Throws()
	{
		// 3002 -> 3003 -> 3002 forms a cycle reachable from root 300.
		var subA = new ActionDefinition(
			3002,
			"A",
			DeployDuration.LongLasting,
			[Selector("link", "enum", 1, 3003)],
			null,
			ActionRole.Subaction);

		var subB = new ActionDefinition(
			3003,
			"B",
			DeployDuration.LongLasting,
			[Selector("back", "enum", 1, 3002)],
			null,
			ActionRole.Subaction);

		var root = new ActionDefinition(
			300,
			"Root",
			DeployDuration.LongLasting,
			[Selector("enter", "enum", 1, 3002)],
			null,
			ActionRole.Action);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[300] = root,
			[3002] = subA,
			[3003] = subB
		};

		var act = () => BuildRegistryWith(actions);

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*Cycle detected*");
	}

	[Fact]
	[Trait("Area", "NestedActions")]
	public void Constructor_ConflictingColumnTypesGraph_Throws()
	{
		// Both subactions contribute a 'shared' column with different property types into one root.
		var subOne = new ActionDefinition(
			7002,
			"One",
			DeployDuration.LongLasting,
			[Column("shared", "percent")],
			null,
			ActionRole.Subaction);

		var subTwo = new ActionDefinition(
			7003,
			"Two",
			DeployDuration.LongLasting,
			[Column("shared", "time")],
			null,
			ActionRole.Subaction);

		var root = new ActionDefinition(
			700,
			"Root",
			DeployDuration.LongLasting,
			[Selector("a", "enum", 1, 7002), Selector("b", "enum", 1, 7003)],
			null,
			ActionRole.Action);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[700] = root,
			[7002] = subOne,
			[7003] = subTwo
		};

		var act = () => BuildRegistryWith(actions);

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*conflicting property types*");
	}

	private static RecipeMetadataRegistry BuildRegistryWith(Dictionary<int, ActionDefinition> actions)
	{
		var config = new AppConfiguration(
			Properties: TestRecipeMetadataRegistryFactory.DefaultStringProperty(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase),
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: new AppUiOptions("ru"));

		return new RecipeMetadataRegistry(config);
	}

	private static ActionPropertyDefinition Column(string key, string propertyTypeId)
	{
		return new ActionPropertyDefinition(key, null, propertyTypeId, null);
	}

	private static ActionPropertyDefinition Selector(
		string key,
		string propertyTypeId,
		int selectorValue,
		int targetId)
	{
		return new ActionPropertyDefinition(
			key,
			"match_mode",
			propertyTypeId,
			null,
			new Dictionary<int, int> { [selectorValue] = targetId });
	}
}
