using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Recipes.Helpers;

[Trait("Component", "Core")]
[Trait("Category", "Unit")]
[Trait("Area", "CellStateResolver")]
public sealed class CellStateResolverTests
{
	[Fact]
	public void IsInapplicable_ReturnsFalse_WhenColumnIsReadOnly()
	{
		var column = BuildColumn(key: "duration", readOnly: true);
		var action = BuildAction(propertyKeys: []);

		var result = CellStateResolver.IsInapplicable(column, action);

		result.Should().BeFalse();
	}

	[Fact]
	public void IsInapplicable_ReturnsFalse_WhenColumnIsActionColumn()
	{
		var column = BuildColumn(key: StepValueParser.ActionColumnKey, readOnly: false);
		var action = BuildAction(propertyKeys: []);

		var result = CellStateResolver.IsInapplicable(column, action);

		result.Should().BeFalse();
	}

	[Fact]
	public void IsInapplicable_ReturnsTrue_WhenActionMissesProperty()
	{
		var column = BuildColumn(key: "temperature", readOnly: false);
		var action = BuildAction(propertyKeys: ["duration"]);

		var result = CellStateResolver.IsInapplicable(column, action);

		result.Should().BeTrue();
	}

	[Fact]
	public void IsInapplicable_ReturnsFalse_WhenActionHasProperty()
	{
		var column = BuildColumn(key: "temperature", readOnly: false);
		var action = BuildAction(propertyKeys: ["temperature"]);

		var result = CellStateResolver.IsInapplicable(column, action);

		result.Should().BeFalse();
	}

	private static GridColumnDefinition BuildColumn(string key, bool readOnly)
	{
		return new GridColumnDefinition(
			Key: key,
			ColumnType: "text",
			UiName: key,
			PropertyTypeId: "string",
			ReadOnly: readOnly,
			SaveToCsv: true);
	}

	private static ActionDefinition BuildAction(string[] propertyKeys)
	{
		var properties = propertyKeys
			.Select(key => new ActionPropertyDefinition(
				Key: key,
				GroupName: null,
				PropertyTypeId: "string",
				DefaultValue: null))
			.ToArray();

		return new ActionDefinition(
			id: 1,
			uiName: "TestAction",
			deployDuration: DeployDuration.Immediate,
			properties: properties);
	}
}
