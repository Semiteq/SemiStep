using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Category", "Unit")]
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
public sealed class ComboBoxItemsProviderTests
{
	private const string ValveGroupId = "valve";
	private const string ModeGroupId = "match_mode";
	private const string UnusedGroupId = "unused";

	private readonly RecipeMetadataRegistry _recipeMetadataRegistry = BuildRegistry();

	[Fact]
	public void ActionItems_EqualPrimaryActionsInRegistryOrder()
	{
		var provider = new ComboBoxItemsProvider(_recipeMetadataRegistry);

		var expected = _recipeMetadataRegistry.GetAllActions()
			.Select(action => new ComboBoxItemViewModel(action.Id, action.UiName));

		provider.ActionItems.Should().Equal(expected);
		provider.ActionItems.Select(item => item.Id).Should().Equal(20, 10, 300);
	}

	[Fact]
	public void EmptyGroupNameOnANonEnumColumn_IsSkipped()
	{
		var emptyGroupAction = new ActionDefinition(
			400,
			"Wait",
			DeployDuration.LongLasting,
			[new ActionPropertyDefinition("step_duration", string.Empty, "float", null)],
			null,
			ActionRole.Action);
		var registry = BuildRegistry(emptyGroupAction);

		var provider = new ComboBoxItemsProvider(registry);

		provider.ActionItems.Select(item => item.Id).Should().Equal(20, 10, 300, 400);
		provider.GetGroupItems(ValveGroupId).Select(item => item.Id).Should().Equal(1, 2);
		provider.GetGroupItems(string.Empty).Should().BeEmpty();
	}

	[Fact]
	public void GroupItems_EqualTheGroupItemsOrderedByKey()
	{
		var provider = new ComboBoxItemsProvider(_recipeMetadataRegistry);

		var expected = _recipeMetadataRegistry.GetGroup(ValveGroupId).Value.Items
			.OrderBy(entry => entry.Key)
			.Select(entry => new ComboBoxItemViewModel(entry.Key, entry.Value));

		provider.GetGroupItems(ValveGroupId).Should().Equal(expected);
		provider.GetGroupItems(ValveGroupId).Select(item => item.Id).Should().Equal(1, 2);
	}

	[Fact]
	public void GroupItems_IncludeGroupsReachedOnlyThroughASubaction()
	{
		var provider = new ComboBoxItemsProvider(_recipeMetadataRegistry);

		var expected = _recipeMetadataRegistry.GetGroup(ModeGroupId).Value.Items
			.OrderBy(entry => entry.Key)
			.Select(entry => new ComboBoxItemViewModel(entry.Key, entry.Value));

		provider.GetGroupItems(ModeGroupId).Should().Equal(expected);
	}

	[Fact]
	public void GroupLookup_IgnoresCase()
	{
		var provider = new ComboBoxItemsProvider(_recipeMetadataRegistry);

		var expected = _recipeMetadataRegistry.GetGroup(ValveGroupId).Value.Items
			.OrderBy(entry => entry.Key)
			.Select(entry => new ComboBoxItemViewModel(entry.Key, entry.Value));

		provider.GetGroupItems("VALVE").Should().Equal(expected);
	}

	[Theory]
	[InlineData(UnusedGroupId)]
	[InlineData("nonexistent_group")]
	public void GroupNoActionReferences_GivesEmptyList(string groupName)
	{
		var provider = new ComboBoxItemsProvider(_recipeMetadataRegistry);

		provider.GetGroupItems(groupName).Should().BeEmpty();
	}

	[Fact]
	public void RepeatedLookups_ReturnTheSameListInstance()
	{
		var provider = new ComboBoxItemsProvider(_recipeMetadataRegistry);

		provider.GetGroupItems(ValveGroupId).Should().BeSameAs(provider.GetGroupItems(ValveGroupId));
	}

	private static RecipeMetadataRegistry BuildRegistry(params ActionDefinition[] extraActions)
	{
		var groups = new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			[ValveGroupId] = Group(ValveGroupId, (2, "Close"), (1, "Open")),
			[ModeGroupId] = Group(ModeGroupId, (1, "Manual"), (0, "Auto")),
			[UnusedGroupId] = Group(UnusedGroupId, (5, "Spare"))
		};

		var subaction = new ActionDefinition(
			3002,
			"ICP manual",
			DeployDuration.LongLasting,
			[new ActionPropertyDefinition("icp_mode", ModeGroupId, "enum", null)],
			null,
			ActionRole.Subaction);

		var root = new ActionDefinition(
			300,
			"Etch",
			DeployDuration.LongLasting,
			[
				new ActionPropertyDefinition(
					"icp_match",
					ValveGroupId,
					"enum",
					null,
					new Dictionary<int, int> { [2] = 3002 })
			],
			null,
			ActionRole.Action);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[20] = Plain(20, "Close"),
			[10] = Plain(10, "Open"),
			[300] = root,
			[3002] = subaction
		};

		foreach (var extraAction in extraActions)
		{
			actions[extraAction.Id] = extraAction;
		}

		return new RecipeMetadataRegistry(new AppConfiguration(
			Properties: TestRecipeMetadataRegistryFactory.DefaultStringProperty(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: groups,
			Actions: actions,
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: new AppUiOptions("ru")));
	}

	private static ActionDefinition Plain(int id, string uiName)
	{
		return new ActionDefinition(
			id,
			uiName,
			DeployDuration.LongLasting,
			[new ActionPropertyDefinition("value", null, "int", null)],
			null,
			ActionRole.Action);
	}

	private static GroupDefinition Group(string groupId, params (int Key, string Text)[] items)
	{
		return new GroupDefinition(groupId, items.ToDictionary(item => item.Key, item => item.Text));
	}
}
