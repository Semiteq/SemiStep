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
		DeployDuration: "longlasting",
		Properties: [_stepDurationProperties, _commentProperties]);

	[Fact]
	public void StepStartTimeColumn_IsReadonly()
	{
		var column = new GridColumnDefinition(
			Key: "step_start_time",
			ColumnType: "step_start_time_field",
			UiName: "Start Time",
			PropertyTypeId: "time",
			PlcDataType: "float",
			ReadOnly: false,
			SaveToCsv: false);

		var result = CellStateResolver.GetCellState(column, _waitAction);

		result.Should().Be(CellState.Readonly);
	}

	[Fact]
	public void UnsupportedColumn_IsDisabled()
	{
		var column = new GridColumnDefinition(
			Key: "unsupported_column",
			ColumnType: "property_field",
			UiName: "Unsupported",
			PropertyTypeId: "float",
			PlcDataType: "float",
			ReadOnly: false,
			SaveToCsv: true);

		var result = CellStateResolver.GetCellState(column, _waitAction);

		result.Should().Be(CellState.Disabled);
	}

	[Fact]
	public void ReadOnlyColumn_IsReadonly()
	{
		var column = new GridColumnDefinition(
			Key: "step_duration",
			ColumnType: "property_field",
			UiName: "Duration",
			PropertyTypeId: "time",
			PlcDataType: "float",
			ReadOnly: true,
			SaveToCsv: true);

		var result = CellStateResolver.GetCellState(column, _waitAction);

		result.Should().Be(CellState.Readonly);
	}

	[Fact]
	public void ActionColumn_IsEnabled()
	{
		var column = new GridColumnDefinition(
			Key: "action",
			ColumnType: "action_combo_box",
			UiName: "Action",
			PropertyTypeId: "enum",
			PlcDataType: "int",
			ReadOnly: false,
			SaveToCsv: true);

		var result = CellStateResolver.GetCellState(column, _waitAction);

		result.Should().Be(CellState.Enabled);
	}
}
