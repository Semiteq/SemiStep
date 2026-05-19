using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Properties;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "PropertyState")]
public sealed class CorePropertyStateTests
{
	private static readonly ActionPropertyDefinition _stepDurationProperty = new(
		Key: "step_duration",
		GroupName: null,
		PropertyTypeId: "time",
		DefaultValue: "10");

	private static readonly ActionPropertyDefinition _commentProperty = new(
		Key: "comment",
		GroupName: null,
		PropertyTypeId: "string",
		DefaultValue: null);

	private static readonly ActionDefinition _waitAction = new(
		id: 10,
		uiName: "Wait",
		deployDuration: DeployDuration.LongLasting,
		properties: [_stepDurationProperty, _commentProperty]);

	[Theory]
	[InlineData("unsupported_column", "property_field", "float", false, true)]
	[InlineData("step_duration", "property_field", "time", true, false)]
	[InlineData("step_start_time", "step_start_time_field", "time", true, false)]
	[InlineData("action", "action_combo_box", "enum", false, false)]
	public void IsInapplicable_ReturnsExpectedValue(
		string key,
		string columnType,
		string propertyTypeId,
		bool readOnly,
		bool expectedInapplicable)
	{
		var column = new GridColumnDefinition(
			Key: key,
			ColumnType: columnType,
			UiName: key,
			PropertyTypeId: propertyTypeId,
			ReadOnly: readOnly,
			SaveToCsv: true);

		var result = CellStateResolver.IsInapplicable(column, _waitAction);

		result.Should().Be(expectedInapplicable);
	}
}
