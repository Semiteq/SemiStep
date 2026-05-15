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
	private static readonly ActionPropertyDefinition _stepDurationProperties = new(
		Key: "step_duration",
		GroupName: null,
		PropertyTypeId: "time",
		DefaultValue: "10");

	private static readonly ActionPropertyDefinition _commentProperties = new(
		Key: "comment",
		GroupName: null,
		PropertyTypeId: "string",
		DefaultValue: null);

	private static readonly ActionDefinition _waitAction = new(
		Id: 10,
		UiName: "Wait",
		DeployDuration: DeployDuration.LongLasting,
		Properties: [_stepDurationProperties, _commentProperties]);

	[Theory]
	[InlineData("unsupported_column", "property_field", "float", false, CellState.Disabled)]
	[InlineData("step_duration", "property_field", "time", true, CellState.Readonly)]
	[InlineData("step_start_time", "step_start_time_field", "time", true, CellState.Readonly)]
	[InlineData("action", "action_combo_box", "enum", false, CellState.Enabled)]
	public void GetCellState_ReturnsExpectedState(
		string key,
		string columnType,
		string propertyTypeId,
		bool readOnly,
		CellState expected)
	{
		var column = new GridColumnDefinition(
			Key: key,
			ColumnType: columnType,
			UiName: key,
			PropertyTypeId: propertyTypeId,
			ReadOnly: readOnly,
			SaveToCsv: true);

		var result = CellStateResolver.GetCellState(column, _waitAction);

		result.Should().Be(expected);
	}
}
