using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "RecipeMetadataRegistry")]
public sealed class RecipeMetadataRegistryTests
{
	private const string ValveGroupId = "valve";

	private static RecipeMetadataRegistry BuildRegistry()
	{
		var groups = new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			[ValveGroupId] = new GroupDefinition(
				GroupId: ValveGroupId,
				Items: new Dictionary<int, string>
				{
					[2] = "Close",
					[1] = "Open"
				})
		};

		var config = new AppConfiguration(
			Properties: new Dictionary<string, PropertyTypeDefinition>(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: groups,
			Actions: new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default);

		return new RecipeMetadataRegistry(config);
	}

	[Fact]
	public void GetComboBoxItems_ReturnsItemsForKnownGroup_OrderedById()
	{
		var registry = BuildRegistry();

		var items = registry.GetComboBoxItems(ValveGroupId);

		items.Should().HaveCount(2);
		items[0].Should().Be(new ComboBoxItemViewModel(1, "Open"));
		items[1].Should().Be(new ComboBoxItemViewModel(2, "Close"));
	}

	[Fact]
	public void GetComboBoxItems_ReturnsEmpty_ForUnknownGroup_WithoutThrowing()
	{
		var registry = BuildRegistry();

		var items = registry.GetComboBoxItems("nonexistent_group");

		items.Should().BeEmpty();
	}

	[Fact]
	public void GetComboBoxItems_ReturnsSameReference_OnRepeatedCalls()
	{
		var registry = BuildRegistry();

		var first = registry.GetComboBoxItems(ValveGroupId);
		var second = registry.GetComboBoxItems(ValveGroupId);

		second.Should().BeSameAs(first);
	}
}
