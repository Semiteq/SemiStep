using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class ColumnWidthCalculatorTests : IAsyncLifetime
{
	private const int LargeCellFontSize = 24;
	private const int LargeHeaderFontSize = 28;
	private const int SampleLength = 12;
	private const string LongWord = "Internationalization";

	private readonly UIFixture _fixture = new();
	private ColumnWidthCalculator _calculator = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_calculator = new ColumnWidthCalculator(_fixture.RecipeMetadataRegistry, GridStyleOptions.Default);
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void HeaderFloor_IsLongestWord_NotWholeHeader()
	{
		var wordOnly = HeaderColumn(LongWord);
		var multiWord = HeaderColumn($"A {LongWord} of the whole process");

		var wordOnlyWidth = GetPixelWidth(_calculator.CalculateColumnWidth(wordOnly));
		var multiWordWidth = GetPixelWidth(_calculator.CalculateColumnWidth(multiWord));

		wordOnlyWidth.Should().BeGreaterThan(_calculator.MinColumnWidth,
			"the longest word must be wide enough to drive the floor");
		multiWordWidth.Should().Be(wordOnlyWidth);
	}

	[AvaloniaFact]
	public void HeaderFloor_IsWiderThanSameTextAsCellContent()
	{
		var actionColumn = PropertyColumn("action", "comment") with
		{
			ColumnType = ColumnTypes.ActionComboBox,
			UiName = ""
		};
		var action = new ActionDefinition(1, LongWord, DeployDuration.Immediate, []);
		var calculator = BuildCalculator(
			[TestPropertyTypeDefinitionBuilder.CreateString("comment", TestRecipeMetadataRegistryFactory.DefaultStringMaxLength)],
			new Dictionary<int, ActionDefinition> { [1] = action },
			new Dictionary<string, GridColumnDefinition> { ["action"] = actionColumn });

		var headerWidth = GetPixelWidth(calculator.CalculateColumnWidth(HeaderColumn(LongWord)));
		var cellWidth = GetPixelWidth(calculator.CalculateColumnWidth(actionColumn));

		headerWidth.Should().BeGreaterThan(cellWidth);
	}

	[AvaloniaFact]
	public void StarColumn_UnaffectedByHeaderFloor()
	{
		var commentColumn = _fixture.RecipeMetadataRegistry.GetColumn("comment").Value with
		{
			UiName = $"{LongWord} of the whole target process parameter"
		};

		var length = _calculator.CalculateColumnWidth(commentColumn);

		length.UnitType.Should().Be(DataGridLengthUnitType.Star);
	}

	[AvaloniaFact]
	public void PropertyField_WithMax_WidensWithLargerMax()
	{
		var comment = TestPropertyTypeDefinitionBuilder.CreateString(
			"comment", TestRecipeMetadataRegistryFactory.DefaultStringMaxLength);
		var small = TestPropertyTypeDefinitionBuilder.CreateFloat("small", max: 100);
		var large = TestPropertyTypeDefinitionBuilder.CreateFloat("large", max: 1_000_000_000);
		var smallColumn = PropertyColumn("small_column", "small");
		var largeColumn = PropertyColumn("large_column", "large");
		var calculator = BuildCalculator(
			[comment, small, large],
			actions: null,
			new Dictionary<string, GridColumnDefinition>
			{
				["small_column"] = smallColumn,
				["large_column"] = largeColumn
			});

		var smallWidth = GetPixelWidth(calculator.CalculateColumnWidth(smallColumn));
		var largeWidth = GetPixelWidth(calculator.CalculateColumnWidth(largeColumn));

		largeWidth.Should().BeGreaterThan(smallWidth);
		largeWidth.Should().BeGreaterThan(calculator.MinColumnWidth);
	}

	[AvaloniaFact]
	public void PropertyField_StringTyped_WidthIgnoresMaxLengthBeyondSampleCap()
	{
		var tinyWidth = StringColumnWidth(1);
		var cappedWidth = StringColumnWidth(SampleLength);
		var longWidth = StringColumnWidth(255);

		longWidth.Should().Be(cappedWidth);
		cappedWidth.Should().BeGreaterThan(tinyWidth);

		static int StringColumnWidth(int maxLength)
		{
			var column = PropertyColumn("text_column", "comment");
			var calculator = BuildCalculator(
				[TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength)],
				actions: null,
				new Dictionary<string, GridColumnDefinition> { ["text_column"] = column });

			return GetPixelWidth(calculator.CalculateColumnWidth(column));
		}
	}

	[AvaloniaFact]
	public void PropertyField_PropertyResolveFails_AppliesHeaderWordFloor()
	{
		var taskColumn = _fixture.RecipeMetadataRegistry.GetColumn("task").Value with { UiName = LongWord };
		var unknownPropertyColumn = taskColumn with { PropertyTypeId = "does_not_exist" };

		var resolvedWidth = GetPixelWidth(_calculator.CalculateColumnWidth(taskColumn));
		var unknownWidth = GetPixelWidth(_calculator.CalculateColumnWidth(unknownPropertyColumn));

		unknownWidth.Should().Be(resolvedWidth,
			"a failed property resolve falls back to the same header-word floor as a resolvable property with no numeric extent");
	}

	[AvaloniaFact]
	public void DispatchDefault_UnknownColumnType_IsFixedWidthAtLeastMinimum()
	{
		var length = _calculator.CalculateColumnWidth(HeaderColumn("X"));

		length.IsStar.Should().BeFalse();
		GetPixelWidth(length).Should().BeGreaterThanOrEqualTo(_calculator.MinColumnWidth);
	}

	[AvaloniaFact]
	public void PropertyField_AggregatesTypesAcrossActionsBoundToKey_WidestUnitWins()
	{
		var comment = TestPropertyTypeDefinitionBuilder.CreateString(
			"comment", TestRecipeMetadataRegistryFactory.DefaultStringMaxLength);
		var flow = TestPropertyTypeDefinitionBuilder.CreateFloat("flow", max: 100) with { Units = "cm3/min" };
		var percent = TestPropertyTypeDefinitionBuilder.CreateFloat("percent", max: 100) with { Units = "%" };

		var valueColumn = PropertyColumn("value", "percent");

		var actions = new Dictionary<int, ActionDefinition>
		{
			[1] = new ActionDefinition(1, "Flow action", DeployDuration.Immediate,
				[new ActionPropertyDefinition("value", null, "flow", null)]),
			[2] = new ActionDefinition(2, "Percent action", DeployDuration.Immediate,
				[new ActionPropertyDefinition("value", null, "percent", null)])
		};

		var calculator = BuildCalculator(
			[comment, flow, percent],
			actions,
			new Dictionary<string, GridColumnDefinition> { ["value"] = valueColumn });
		var defaultOnlyCalculator = BuildCalculator(
			[comment, flow, percent],
			actions: null,
			new Dictionary<string, GridColumnDefinition> { ["value"] = valueColumn });

		var pixelWidth = GetPixelWidth(calculator.CalculateColumnWidth(valueColumn));
		var defaultOnlyWidth = GetPixelWidth(defaultOnlyCalculator.CalculateColumnWidth(valueColumn));

		pixelWidth.Should().BeGreaterThan(defaultOnlyWidth,
			"the wider 'cm3/min' type is reachable only through an action binding, not the column default");
	}

	[AvaloniaFact]
	public void PropertyField_NegativeMinWiderThanMax_SizesFromMinRepresentative()
	{
		var comment = TestPropertyTypeDefinitionBuilder.CreateString(
			"comment", TestRecipeMetadataRegistryFactory.DefaultStringMaxLength);
		var speed = TestPropertyTypeDefinitionBuilder.CreateFloat("speed", min: -100, max: 100) with { Units = "%/min" };
		var speedWithoutNegativeMin =
			TestPropertyTypeDefinitionBuilder.CreateFloat("speed_positive", max: 100) with { Units = "%/min" };

		var speedColumn = PropertyColumn("speed", "speed");
		var maxDrivenColumn = PropertyColumn("speed_positive", "speed_positive");

		var calculator = BuildCalculator(
			[comment, speed, speedWithoutNegativeMin],
			actions: null,
			new Dictionary<string, GridColumnDefinition>
			{
				["speed"] = speedColumn,
				["speed_positive"] = maxDrivenColumn
			});

		var negativeMinWidth = GetPixelWidth(calculator.CalculateColumnWidth(speedColumn));
		var maxDrivenWidth = GetPixelWidth(calculator.CalculateColumnWidth(maxDrivenColumn));

		negativeMinWidth.Should().BeGreaterThan(maxDrivenWidth,
			"the negative Min is the widest representative, so it drives the width beyond the Max-driven twin");
	}

	[AvaloniaFact]
	public void LargerFonts_WidenColumnAndMinimum()
	{
		var durationColumn = _fixture.RecipeMetadataRegistry.GetColumn("step_duration").Value;
		var largeFontCalculator = new ColumnWidthCalculator(_fixture.RecipeMetadataRegistry, LargeFontStyle());

		var smallFontWidth = GetPixelWidth(_calculator.CalculateColumnWidth(durationColumn));
		var largeFontWidth = GetPixelWidth(largeFontCalculator.CalculateColumnWidth(durationColumn));

		largeFontWidth.Should().BeGreaterThan(smallFontWidth);
		largeFontCalculator.MinColumnWidth.Should().BeGreaterThan(_calculator.MinColumnWidth);
	}

	[AvaloniaFact]
	public void ComboColumn_WiderThanContentColumn_ByChevronBudget()
	{
		var (calculator, comboColumn, contentColumn) = BuildComboAndContent(GridStyleOptions.Default);

		var comboWidth = GetPixelWidth(calculator.CalculateColumnWidth(comboColumn));
		var contentWidth = GetPixelWidth(calculator.CalculateColumnWidth(contentColumn));

		comboWidth.Should().BeGreaterThan(contentWidth);
	}

	[AvaloniaFact]
	public void ComboChevronBudget_DoesNotScaleWithFont_WhileContentChromeDoes()
	{
		var (smallCalculator, comboColumn, contentColumn) = BuildComboAndContent(GridStyleOptions.Default);
		var (largeCalculator, _, _) = BuildComboAndContent(LargeFontStyle());

		var comboGrowth = GetPixelWidth(largeCalculator.CalculateColumnWidth(comboColumn))
			- GetPixelWidth(smallCalculator.CalculateColumnWidth(comboColumn));
		var contentGrowth = GetPixelWidth(largeCalculator.CalculateColumnWidth(contentColumn))
			- GetPixelWidth(smallCalculator.CalculateColumnWidth(contentColumn));

		comboGrowth.Should().BeLessThan(contentGrowth,
			"both columns grow with the text, but only the content column also grows its font-proportional chrome");
	}

	private static (ColumnWidthCalculator Calculator, GridColumnDefinition Combo, GridColumnDefinition Content)
		BuildComboAndContent(GridStyleOptions style)
	{
		var sampleText = new string('0', SampleLength);
		var sampleProperty = TestPropertyTypeDefinitionBuilder.CreateString("sample", SampleLength);
		var comboColumn = new GridColumnDefinition(
			Key: "value",
			ColumnType: ColumnTypes.ActionComboBox,
			UiName: "X",
			PropertyTypeId: "sample",
			ReadOnly: false,
			SaveToCsv: true);
		var contentColumn = comboColumn with { ColumnType = ColumnTypes.PropertyField };
		var actions = new Dictionary<int, ActionDefinition>
		{
			[1] = new ActionDefinition(1, sampleText, DeployDuration.Immediate,
				[new ActionPropertyDefinition("value", null, "sample", null)])
		};
		var registry = TestRecipeMetadataRegistryFactory.Build(
			[sampleProperty],
			actions,
			columns: new Dictionary<string, GridColumnDefinition> { ["combo"] = comboColumn, ["content"] = contentColumn });

		return (new ColumnWidthCalculator(registry, style), comboColumn, contentColumn);
	}

	private GridColumnDefinition HeaderColumn(string header)
	{
		return _fixture.RecipeMetadataRegistry.GetColumn("task").Value with
		{
			ColumnType = "not_a_real_type",
			UiName = header
		};
	}

	private static GridColumnDefinition PropertyColumn(string key, string propertyTypeId)
	{
		return new GridColumnDefinition(
			Key: key,
			ColumnType: ColumnTypes.PropertyField,
			UiName: "V",
			PropertyTypeId: propertyTypeId,
			ReadOnly: false,
			SaveToCsv: true);
	}

	private static GridStyleOptions LargeFontStyle()
	{
		var fonts = GridStyleOptions.Default.Fonts with
		{
			CellFontSize = LargeCellFontSize,
			HeaderFontSize = LargeHeaderFontSize
		};

		return GridStyleOptions.Default with { Fonts = fonts };
	}

	private static ColumnWidthCalculator BuildCalculator(
		IEnumerable<PropertyTypeDefinition> properties,
		Dictionary<int, ActionDefinition>? actions,
		Dictionary<string, GridColumnDefinition> columns)
	{
		var registry = TestRecipeMetadataRegistryFactory.Build(properties, actions, columns: columns);

		return new ColumnWidthCalculator(registry, GridStyleOptions.Default);
	}

	private static int GetPixelWidth(DataGridLength length)
	{
		return (int)length.Value;
	}
}
