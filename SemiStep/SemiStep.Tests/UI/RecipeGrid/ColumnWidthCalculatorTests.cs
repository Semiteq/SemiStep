using System.Globalization;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using FluentAssertions;

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
	// Mirrors the private MaxStringSampleLength cap in ColumnWidthCalculator.
	private const int StringSampleCap = 12;

	// Mirrors the private CellChromeAllowance in ColumnWidthCalculator.
	private const double CellChromeAllowance = 26;

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
	public void ActionColumn_NoSortIconReservation_WidthBelowOldSortIconFormula()
	{
		var actionColumn = _fixture.RecipeMetadataRegistry.GetColumn("action").Value;

		var pixelWidth = GetPixelWidth(_calculator.CalculateColumnWidth(actionColumn));

		pixelWidth.Should().BeGreaterThanOrEqualTo(ColumnWidthCalculator.MinColumnWidth);

		var contentWidth = MeasureActionContentWidth();
		var oldFormulaWidth = (int)Math.Ceiling((contentWidth + 32) * 1.4);
		pixelWidth.Should().BeLessThan(oldFormulaWidth);

		var expectedWidth = ExpectedWidth(contentWidth, actionColumn.UiName);
		pixelWidth.Should().Be(expectedWidth);
	}

	[AvaloniaFact]
	public void LongHeader_DoesNotInflateColumnBeyondContent()
	{
		var actionColumn = _fixture.RecipeMetadataRegistry.GetColumn("action").Value;
		var longHeaderColumn = actionColumn with
		{
			UiName = "An extremely long header label that would otherwise widen this column dramatically"
		};

		var contentWidth = MeasureActionContentWidth();
		var longHeaderWidth = GetPixelWidth(_calculator.CalculateColumnWidth(longHeaderColumn));

		var expectedWidth = ExpectedWidth(contentWidth, longHeaderColumn.UiName);
		longHeaderWidth.Should().Be(expectedWidth,
			"only the longest header word floors the width, not the whole header");
		longHeaderWidth.Should().BeLessThanOrEqualTo(
			(int)Math.Ceiling(Math.Max(contentWidth + CellChromeAllowance, LongestHeaderWordFloor(longHeaderColumn.UiName))));
	}

	[AvaloniaFact]
	public void MultiWordHeader_FloorsAtLongestWord()
	{
		var header = "Начальное значение";
		var taskColumn = _fixture.RecipeMetadataRegistry.GetColumn("task").Value with
		{
			UiName = header
		};

		var pixelWidth = GetPixelWidth(_calculator.CalculateColumnWidth(taskColumn));

		var longestWord = header
			.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
			.OrderByDescending(word => MeasureText(word, GridStyleOptions.Default.HeaderFontSize, FontWeight.Bold))
			.First();
		var longestWordFloor = (int)Math.Ceiling(
			MeasureText(longestWord, GridStyleOptions.Default.HeaderFontSize, FontWeight.Bold) + CellChromeAllowance);
		var wholeHeaderWidth = (int)Math.Ceiling(
			MeasureText(header, GridStyleOptions.Default.HeaderFontSize, FontWeight.Bold) + CellChromeAllowance);

		pixelWidth.Should().Be(longestWordFloor,
			"empty content makes the width equal the longest-word floor exactly");
		pixelWidth.Should().BeLessThan(wholeHeaderWidth,
			"the floor is the longest WORD, never the whole single-line header");
	}

	[AvaloniaFact]
	public void HeaderFloorMeasuredBoldAtHeaderFontSize()
	{
		var word = "Начальное";

		var boldHeaderFont = MeasureText(word, GridStyleOptions.Default.HeaderFontSize, FontWeight.Bold);
		var normalCellFont = MeasureText(word, GridStyleOptions.Default.CellFontSize, FontWeight.Normal);

		boldHeaderFont.Should().BeGreaterThan(normalCellFont,
			"the header-word floor must be measured bold at HeaderFontSize, not at the cell font");
	}

	[AvaloniaFact]
	public void LongestWordFloor_FarBelowWholeHeaderWidth()
	{
		var header = "An extremely long header label that would otherwise widen this column";

		var longestWordFloor = LongestHeaderWordFloor(header);
		var wholeHeaderWidth = MeasureText(header, GridStyleOptions.Default.HeaderFontSize, FontWeight.Bold)
			+ CellChromeAllowance;

		longestWordFloor.Should().BeLessThan(wholeHeaderWidth / 2,
			"the floor is the longest WORD, not the whole single-line header (FullHD safety)");
	}

	[AvaloniaFact]
	public void StarColumn_UnaffectedByHeaderFloor()
	{
		var commentColumn = _fixture.RecipeMetadataRegistry.GetColumn("comment").Value with
		{
			UiName = "Начальное значение целевого параметра процесса"
		};

		var length = _calculator.CalculateColumnWidth(commentColumn);

		length.IsStar.Should().BeTrue("the star column absorbs remaining space and ignores the header floor");
		length.UnitType.Should().Be(DataGridLengthUnitType.Star);
	}

	[AvaloniaFact]
	public void PropertyField_NoNumericExtent_AppliesHeaderWordFloor()
	{
		var taskColumn = _fixture.RecipeMetadataRegistry.GetColumn("task").Value with
		{
			UiName = "Длительность"
		};

		var headerWordFloor = ExpectedWidth(0, taskColumn.UiName);
		headerWordFloor.Should().BeGreaterThan(ColumnWidthCalculator.MinColumnWidth,
			"the chosen header's longest word must exceed MinColumnWidth so the floor is load-bearing");

		var pixelWidth = GetPixelWidth(_calculator.CalculateColumnWidth(taskColumn));

		pixelWidth.Should().Be(headerWordFloor);
	}

	[AvaloniaFact]
	public void PropertyField_WithMax_SizesFromMaxRepresentative()
	{
		var durationColumn = _fixture.RecipeMetadataRegistry.GetColumn("step_duration").Value;
		var durationProperty = _fixture.RecipeMetadataRegistry.GetProperty(durationColumn.PropertyTypeId).Value;
		durationProperty.Max.Should().NotBeNull(
			"the test configuration must declare Max for this assertion to be meaningful");

		var pixelWidth = GetPixelWidth(_calculator.CalculateColumnWidth(durationColumn));

		var representative = TimeFormatHelper.FormatValue(
			durationProperty.Max!.Value.ToString(CultureInfo.InvariantCulture),
			durationProperty.FormatKind,
			durationProperty.Units);
		var contentWidth = MeasureText(representative, GridStyleOptions.Default.CellFontSize, FontWeight.Normal);
		var expectedWidth = ExpectedWidth(contentWidth, durationColumn.UiName);

		pixelWidth.Should().Be(expectedWidth);
		pixelWidth.Should().BeGreaterThan(ColumnWidthCalculator.MinColumnWidth,
			"the formatted Max value is wider than the floor");
	}

	[AvaloniaFact]
	public void PropertyField_StringTyped_SizesFromCappedSampleNotFullMaxLength()
	{
		var stringProperty = _fixture.RecipeMetadataRegistry.GetProperty("string").Value;
		stringProperty.MaxLength.Should().NotBeNull();
		stringProperty.MaxLength!.Value.Should().BeGreaterThan(StringSampleCap,
			"the assertion is only meaningful when MaxLength exceeds the cap");

		var stringColumn = _fixture.RecipeMetadataRegistry.GetColumn("task").Value with
		{
			PropertyTypeId = "string"
		};

		var pixelWidth = GetPixelWidth(_calculator.CalculateColumnWidth(stringColumn));

		var cappedRepresentative = new string('0', StringSampleCap);
		var cappedContentWidth = MeasureText(cappedRepresentative, GridStyleOptions.Default.CellFontSize, FontWeight.Normal);
		var expectedWidth = ExpectedWidth(cappedContentWidth, stringColumn.UiName);

		pixelWidth.Should().Be(expectedWidth);
		pixelWidth.Should().BeGreaterThanOrEqualTo(ColumnWidthCalculator.MinColumnWidth);

		var fullLengthRepresentative = new string('0', stringProperty.MaxLength!.Value);
		var fullLengthWidth = (int)Math.Ceiling(
			MeasureText(fullLengthRepresentative, GridStyleOptions.Default.CellFontSize, FontWeight.Normal) + CellChromeAllowance);
		pixelWidth.Should().BeLessThan(fullLengthWidth,
			"the cap must keep the column far below the full MaxLength width");
	}

	[AvaloniaFact]
	public void PropertyField_PropertyResolveFails_AppliesHeaderWordFloor()
	{
		var taskColumn = _fixture.RecipeMetadataRegistry.GetColumn("task").Value with
		{
			UiName = "Длительность"
		};
		var unknownPropertyColumn = taskColumn with { PropertyTypeId = "does_not_exist" };

		var headerWordFloor = ExpectedWidth(0, unknownPropertyColumn.UiName);
		headerWordFloor.Should().BeGreaterThan(ColumnWidthCalculator.MinColumnWidth,
			"the chosen header's longest word must exceed MinColumnWidth so the floor is load-bearing");

		var pixelWidth = GetPixelWidth(_calculator.CalculateColumnWidth(unknownPropertyColumn));

		pixelWidth.Should().Be(headerWordFloor);
	}

	[AvaloniaFact]
	public void DispatchDefault_UnknownColumnType_AppliesHeaderWordFloor()
	{
		var unknownTypeColumn = _fixture.RecipeMetadataRegistry.GetColumn("task").Value with
		{
			ColumnType = "not_a_real_type",
			UiName = "Длительность"
		};

		var headerWordFloor = ExpectedWidth(0, unknownTypeColumn.UiName);
		headerWordFloor.Should().BeGreaterThan(ColumnWidthCalculator.MinColumnWidth,
			"the chosen header's longest word must exceed MinColumnWidth so the floor is load-bearing");

		var length = _calculator.CalculateColumnWidth(unknownTypeColumn);

		length.IsStar.Should().BeFalse("the dispatch default is a floored fixed width, never Star");
		GetPixelWidth(length).Should().Be(headerWordFloor);
	}

	[AvaloniaFact]
	public void PropertyField_AggregatesTypesAcrossActionsBoundToKey_WidestUnitWins()
	{
		var comment = TestPropertyTypeDefinitionBuilder.CreateString(
			"comment", TestRecipeMetadataRegistryFactory.DefaultStringMaxLength);
		var flow = TestPropertyTypeDefinitionBuilder.CreateFloat("flow", max: 100) with { Units = "см³/мин" };
		var percent = TestPropertyTypeDefinitionBuilder.CreateFloat("percent", max: 100) with { Units = "%" };

		var valueColumn = new GridColumnDefinition(
			Key: "value",
			ColumnType: ColumnTypes.PropertyField,
			UiName: "Значение",
			PropertyTypeId: "percent",
			ReadOnly: false,
			SaveToCsv: true);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[1] = new ActionDefinition(1, "Flow action", DeployDuration.Immediate,
				new[] { new ActionPropertyDefinition("value", null, "flow", null) }),
			[2] = new ActionDefinition(2, "Percent action", DeployDuration.Immediate,
				new[] { new ActionPropertyDefinition("value", null, "percent", null) })
		};

		var calculator = BuildCalculator(
			new[] { comment, flow, percent },
			actions,
			new Dictionary<string, GridColumnDefinition> { ["value"] = valueColumn });

		var pixelWidth = GetPixelWidth(calculator.CalculateColumnWidth(valueColumn));

		var flowWidth = ExpectedWidth(MeasureRepresentative(flow, flow.Max!.Value), valueColumn.UiName);
		var percentWidth = ExpectedWidth(MeasureRepresentative(percent, percent.Max!.Value), valueColumn.UiName);

		pixelWidth.Should().Be(flowWidth,
			"the column sizes to the widest unit-bearing value any action binds to its key");
		flowWidth.Should().BeGreaterThan(percentWidth,
			"the wider 'см³/мин' type is reachable only through an action binding, not the column default");
	}

	[AvaloniaFact]
	public void PropertyField_NegativeMinWiderThanMax_SizesFromMinRepresentative()
	{
		var comment = TestPropertyTypeDefinitionBuilder.CreateString(
			"comment", TestRecipeMetadataRegistryFactory.DefaultStringMaxLength);
		var speed = TestPropertyTypeDefinitionBuilder.CreateFloat("speed", min: -100, max: 100) with { Units = "%/мин" };

		var speedColumn = new GridColumnDefinition(
			Key: "speed",
			ColumnType: ColumnTypes.PropertyField,
			UiName: "V",
			PropertyTypeId: "speed",
			ReadOnly: false,
			SaveToCsv: true);

		var calculator = BuildCalculator(
			new[] { comment, speed },
			actions: null,
			new Dictionary<string, GridColumnDefinition> { ["speed"] = speedColumn });

		var pixelWidth = GetPixelWidth(calculator.CalculateColumnWidth(speedColumn));

		var minWidth = ExpectedWidth(MeasureRepresentative(speed, speed.Min!.Value), speedColumn.UiName);
		var maxWidth = ExpectedWidth(MeasureRepresentative(speed, speed.Max!.Value), speedColumn.UiName);

		pixelWidth.Should().Be(minWidth,
			"the negative Min ('-100 %/мин') is the widest representative and drives the width");
		minWidth.Should().BeGreaterThan(maxWidth,
			"the leading minus makes Min wider than Max for a symmetric range");
	}

	private static ColumnWidthCalculator BuildCalculator(
		IEnumerable<PropertyTypeDefinition> properties,
		Dictionary<int, ActionDefinition>? actions,
		Dictionary<string, GridColumnDefinition> columns)
	{
		var registry = TestRecipeMetadataRegistryFactory.Build(properties, actions, columns: columns);

		return new ColumnWidthCalculator(registry, GridStyleOptions.Default);
	}

	private static double MeasureRepresentative(PropertyTypeDefinition property, double extent)
	{
		var representative = TimeFormatHelper.FormatValue(
			extent.ToString(CultureInfo.InvariantCulture),
			property.FormatKind,
			property.Units);

		return MeasureText(representative, GridStyleOptions.Default.CellFontSize, FontWeight.Normal);
	}

	private static int ExpectedWidth(double contentWidth, string headerText)
	{
		var contentBudget = contentWidth + CellChromeAllowance;
		return (int)Math.Ceiling(
			Math.Max(Math.Max(contentBudget, LongestHeaderWordFloor(headerText)), ColumnWidthCalculator.MinColumnWidth));
	}

	private static double LongestHeaderWordFloor(string header)
	{
		if (string.IsNullOrWhiteSpace(header))
		{
			return 0;
		}

		var words = header.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

		var maxWordWidth = 0.0;
		foreach (var word in words)
		{
			var wordWidth = MeasureText(word, GridStyleOptions.Default.HeaderFontSize, FontWeight.Bold);
			if (wordWidth > maxWordWidth)
			{
				maxWordWidth = wordWidth;
			}
		}

		return maxWordWidth + CellChromeAllowance;
	}

	private double MeasureActionContentWidth()
	{
		var maxWidth = 0.0;
		foreach (var action in _fixture.RecipeMetadataRegistry.GetAllActions())
		{
			var width = MeasureText(action.UiName, GridStyleOptions.Default.CellFontSize, FontWeight.Normal);
			if (width > maxWidth)
			{
				maxWidth = width;
			}
		}

		return maxWidth;
	}

	private static double MeasureText(string text, double fontSize, FontWeight fontWeight)
	{
		var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, fontWeight);
		var formattedText = new FormattedText(
			text,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			typeface,
			fontSize,
			Brushes.Black);

		return formattedText.WidthIncludingTrailingWhitespace;
	}

	private static int GetPixelWidth(DataGridLength length)
	{
		return (int)length.Value;
	}
}
