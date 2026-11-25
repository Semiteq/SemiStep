using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "TimeFormat")]
[Trait("Category", "Unit")]
public sealed class TimeFormatHelperTests
{
	[Fact]
	public void TimeHmsValue_FormatsWithoutUnits()
	{
		TimeFormatHelper.FormatValue("600", TimeFormatHelper.TimeHmsFormat, "с").Should().Be("00:10:00");
	}

	[Fact]
	public void NumericValue_KeepsItsUnit()
	{
		TimeFormatHelper.FormatValue("12.5", TimeFormatHelper.DefaultFormatKind, "В").Should().Be("12.5 В");
	}

	[AvaloniaFact]
	public void StepStartTimeColumn_MeasuresWithoutTheDurationUnit()
	{
		var withUnit = StartTimeColumnWidth(durationUnits: "секунд");
		var withoutUnit = StartTimeColumnWidth(durationUnits: "");

		withUnit.Should().Be(withoutUnit);
	}

	private static double StartTimeColumnWidth(string durationUnits)
	{
		var duration = TestPropertyTypeDefinitionBuilder.CreateFloat("time") with
		{
			Units = durationUnits,
			FormatKind = TimeFormatHelper.TimeHmsFormat
		};
		var durationColumn = new GridColumnDefinition(
			Key: StepValueParser.StepDurationColumnKey,
			ColumnType: ColumnTypes.PropertyField,
			UiName: "T",
			PropertyTypeId: "time",
			ReadOnly: false,
			SaveToCsv: true);
		var startTimeColumn = new GridColumnDefinition(
			Key: TimeFormatHelper.StepStartTimeColumnKey,
			ColumnType: ColumnTypes.StepStartTimeField,
			UiName: "S",
			PropertyTypeId: "time",
			ReadOnly: true,
			SaveToCsv: false);
		var registry = TestRecipeMetadataRegistryFactory.Build(
			[duration, .. TestRecipeMetadataRegistryFactory.DefaultStringProperty().Values],
			columns: new Dictionary<string, GridColumnDefinition>
			{
				[durationColumn.Key] = durationColumn,
				[startTimeColumn.Key] = startTimeColumn
			});

		return new ColumnWidthCalculator(registry, GridStyleOptions.Default).CalculateColumnWidth(startTimeColumn).Value;
	}
}
