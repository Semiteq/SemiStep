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

	// Mirror the font-proportional chrome of ColumnWidthCalculator, derived from GridStyleOptions.Default.
	private const double ChromeFontMultiple = 2.0;
	private const double MinColumnWidthEms = 6.0;
	private const double ComboBoxChromeWidth = 38;

	// Larger fonts for the font-scaling tests.
	private const int LargeCellFontSize = 24;
	private const int LargeHeaderFontSize = 28;

	private static int ContentChrome =>
		(int)Math.Ceiling(GridStyleOptions.Default.Fonts.CellFontSize * ChromeFontMultiple);

	private static int HeaderFloorChrome =>
		(int)Math.Ceiling(GridStyleOptions.Default.Fonts.HeaderFontSize * ChromeFontMultiple);

	private static int MinColumnWidthFloor =>
		(int)Math.Ceiling(GridStyleOptions.Default.Fonts.CellFontSize * MinColumnWidthEms);

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

		pixelWidth.Should().BeGreaterThanOrEqualTo(MinColumnWidthFloor);

		var contentWidth = MeasureActionContentWidth();
		var oldFormulaWidth = (int)Math.Ceiling((contentWidth + 32) * 1.4);
		pixelWidth.Should().BeLessThan(oldFormulaWidth);
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

		longHeaderWidth.Should().BeLessThanOrEqualTo(
			(int)Math.Ceiling(Math.Max(contentWidth + ComboBoxChromeWidth, LongestHeaderWordFloor(longHeaderColumn.UiName))),
			"only the longest header word floors the width, not the whole header");
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

		var wholeHeaderWidth = (int)Math.Ceiling(
			MeasureText(header, GridStyleOptions.Default.Fonts.HeaderFontSize, FontWeight.Bold) + HeaderFloorChrome);

		pixelWidth.Should().BeLessThan(wholeHeaderWidth,
			"the floor is the longest WORD, never the whole single-line header");
	}

	[AvaloniaFact]
	public void HeaderFloorMeasuredBoldAtHeaderFontSize()
	{
		var word = "Начальное";

		var boldHeaderFont = MeasureText(word, GridStyleOptions.Default.Fonts.HeaderFontSize, FontWeight.Bold);
		var normalCellFont = MeasureText(word, GridStyleOptions.Default.Fonts.CellFontSize, FontWeight.Normal);

		boldHeaderFont.Should().BeGreaterThan(normalCellFont,
			"the header-word floor must be measured bold at HeaderFontSize, not at the cell font");
	}

	[AvaloniaFact]
	public void LongestWordFloor_FarBelowWholeHeaderWidth()
	{
		var header = "An extremely long header label that would otherwise widen this column";

		var longestWordFloor = LongestHeaderWordFloor(header);
		var wholeHeaderWidth = MeasureText(header, GridStyleOptions.Default.Fonts.HeaderFontSize, FontWeight.Bold)
			+ HeaderFloorChrome;

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
		var contentWidth = MeasureText(representative, GridStyleOptions.Default.Fonts.CellFontSize, FontWeight.Normal);

		pixelWidth.Should().BeGreaterThan((int)contentWidth,
			"the column must fully cover the formatted Max representative");
		pixelWidth.Should().BeGreaterThan(MinColumnWidthFloor,
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
		var cappedContentWidth = MeasureText(cappedRepresentative, GridStyleOptions.Default.Fonts.CellFontSize, FontWeight.Normal);

		pixelWidth.Should().BeGreaterThan((int)cappedContentWidth,
			"the column must fully cover the capped sample");
		pixelWidth.Should().BeGreaterThanOrEqualTo(MinColumnWidthFloor);

		var fullLengthRepresentative = new string('0', stringProperty.MaxLength!.Value);
		var fullLengthWidth = (int)Math.Ceiling(
			MeasureText(fullLengthRepresentative, GridStyleOptions.Default.Fonts.CellFontSize, FontWeight.Normal) + ContentChrome);
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

		var resolvedWidth = GetPixelWidth(_calculator.CalculateColumnWidth(taskColumn));
		var unknownWidth = GetPixelWidth(_calculator.CalculateColumnWidth(unknownPropertyColumn));

		unknownWidth.Should().Be(resolvedWidth,
			"a failed property resolve falls back to the same header-word floor as a resolvable property with no numeric extent");
	}

	[AvaloniaFact]
	public void DispatchDefault_UnknownColumnType_AppliesHeaderWordFloor()
	{
		var unknownTypeColumn = _fixture.RecipeMetadataRegistry.GetColumn("task").Value with
		{
			ColumnType = "not_a_real_type",
			UiName = "Длительность"
		};

		var length = _calculator.CalculateColumnWidth(unknownTypeColumn);

		length.IsStar.Should().BeFalse("the dispatch default is a floored fixed width, never Star");
		GetPixelWidth(length).Should().BeGreaterThanOrEqualTo(MinColumnWidthFloor,
			"the dispatch default applies the minimum-width floor");
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
		var defaultOnlyCalculator = BuildCalculator(
			new[] { comment, flow, percent },
			actions: null,
			new Dictionary<string, GridColumnDefinition> { ["value"] = valueColumn });

		var pixelWidth = GetPixelWidth(calculator.CalculateColumnWidth(valueColumn));
		var defaultOnlyWidth = GetPixelWidth(defaultOnlyCalculator.CalculateColumnWidth(valueColumn));

		pixelWidth.Should().BeGreaterThan(defaultOnlyWidth,
			"the wider 'см³/мин' type is reachable only through an action binding, not the column default");
		pixelWidth.Should().BeGreaterThan((int)MeasureRepresentative(flow, flow.Max!.Value),
			"the column sizes to the widest unit-bearing value any action binds to its key");
	}

	[AvaloniaFact]
	public void PropertyField_NegativeMinWiderThanMax_SizesFromMinRepresentative()
	{
		var comment = TestPropertyTypeDefinitionBuilder.CreateString(
			"comment", TestRecipeMetadataRegistryFactory.DefaultStringMaxLength);
		var speed = TestPropertyTypeDefinitionBuilder.CreateFloat("speed", min: -100, max: 100) with { Units = "%/мин" };
		var speedWithoutNegativeMin =
			TestPropertyTypeDefinitionBuilder.CreateFloat("speed_positive", max: 100) with { Units = "%/мин" };

		var speedColumn = new GridColumnDefinition(
			Key: "speed",
			ColumnType: ColumnTypes.PropertyField,
			UiName: "V",
			PropertyTypeId: "speed",
			ReadOnly: false,
			SaveToCsv: true);
		var maxDrivenColumn = speedColumn with { Key = "speed_positive", PropertyTypeId = "speed_positive" };

		var calculator = BuildCalculator(
			new[] { comment, speed, speedWithoutNegativeMin },
			actions: null,
			new Dictionary<string, GridColumnDefinition>
			{
				["speed"] = speedColumn,
				["speed_positive"] = maxDrivenColumn
			});

		var negativeMinWidth = GetPixelWidth(calculator.CalculateColumnWidth(speedColumn));
		var maxDrivenWidth = GetPixelWidth(calculator.CalculateColumnWidth(maxDrivenColumn));

		negativeMinWidth.Should().BeGreaterThan(maxDrivenWidth,
			"the negative Min ('-100 %/мин') is the widest representative, so it drives the width beyond the Max-driven twin");
		negativeMinWidth.Should().BeGreaterThan((int)MeasureRepresentative(speed, speed.Min!.Value),
			"the column must fully cover the Min representative");
	}

	[AvaloniaFact]
	public void Chrome_ScalesWithFont_ChromeRemainderGrowsBeyondContentScaling()
	{
		var smallCellFont = GridStyleOptions.Default.Fonts.CellFontSize;
		var largeCellFont = LargeCellFontSize;
		var durationColumn = _fixture.RecipeMetadataRegistry.GetColumn("step_duration").Value;

		var smallFontCalculator = new ColumnWidthCalculator(
			_fixture.RecipeMetadataRegistry, GridStyleOptions.Default);
		var largeFontCalculator = new ColumnWidthCalculator(
			_fixture.RecipeMetadataRegistry,
			GridStyleOptions.Default with { Fonts = GridStyleOptions.Default.Fonts with { CellFontSize = largeCellFont, HeaderFontSize = LargeHeaderFontSize } });

		var smallFontWidth = GetPixelWidth(smallFontCalculator.CalculateColumnWidth(durationColumn));
		var largeFontWidth = GetPixelWidth(largeFontCalculator.CalculateColumnWidth(durationColumn));

		var representative = TimeFormatHelper.FormatValue(
			"0", TimeFormatHelper.TimeHmsFormat, TimeFormatHelper.TimeUnits);
		var smallContent = MeasureText(representative, smallCellFont, FontWeight.Normal);
		var largeContent = MeasureText(representative, largeCellFont, FontWeight.Normal);

		var smallChromeRemainder = smallFontWidth - smallContent;
		var largeChromeRemainder = largeFontWidth - largeContent;

		largeChromeRemainder.Should().BeGreaterThan(smallChromeRemainder,
			"the chrome reserve is a font multiple, so at a larger font the width grows by MORE than pure content scaling");

		largeFontCalculator.MinColumnWidth.Should().BeGreaterThan(smallFontCalculator.MinColumnWidth,
			"the minimum-width floor is a font multiple, so it grows with the cell font");
	}

	[AvaloniaFact]
	public void Typefaces_ThreadConfiguredFamilyWeightAndItalic_PerRole()
	{
		// The headless text shaper measures by glyph count and size only, so weight/family/italic
		// produce identical widths; assert the role typefaces the measurement uses instead, which
		// fails if a regression drops family/weight/italic from the measured Typeface.
		var defaults = GridStyleOptions.Default;
		var style = defaults with
		{
			Fonts = defaults.Fonts with
			{
				FontFamily = "Courier New",
				CellFontWeight = 600,
				CellItalic = true,
				HeaderFontWeight = 900,
				HeaderItalic = false
			}
		};
		var calculator = new ColumnWidthCalculator(_fixture.RecipeMetadataRegistry, style);

		calculator.CellTypeface.FontFamily.Name.Should().Be("Courier New");
		calculator.CellTypeface.Weight.Should().Be((FontWeight)600);
		calculator.CellTypeface.Style.Should().Be(FontStyle.Italic);

		calculator.HeaderTypeface.FontFamily.Name.Should().Be("Courier New");
		calculator.HeaderTypeface.Weight.Should().Be((FontWeight)900);
		calculator.HeaderTypeface.Style.Should().Be(FontStyle.Normal);
	}

	[AvaloniaFact]
	public void ComboColumn_WiderThanContentColumn_ByChevronBudget()
	{
		var representative = new string('0', StringSampleCap);
		var sampleProperty = TestPropertyTypeDefinitionBuilder.CreateString("sample", StringSampleCap);

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
			[1] = new ActionDefinition(1, representative, DeployDuration.Immediate,
				new[] { new ActionPropertyDefinition("value", null, "sample", null) })
		};

		var calculator = BuildCalculator(
			new[] { sampleProperty },
			actions,
			new Dictionary<string, GridColumnDefinition>
			{
				["combo"] = comboColumn,
				["content"] = contentColumn
			});

		var comboWidth = GetPixelWidth(calculator.CalculateColumnWidth(comboColumn));
		var contentWidth = GetPixelWidth(calculator.CalculateColumnWidth(contentColumn));

		var delta = comboWidth - contentWidth;

		delta.Should().BeGreaterThan(0,
			"the combo path budgets the wider Semi ComboBox chrome, so the combo column exceeds the content column");
	}

	[AvaloniaFact]
	public void ComboChrome_StaysConstantAcrossFonts_WhileContentChromeGrows()
	{
		var smallCellFont = GridStyleOptions.Default.Fonts.CellFontSize;
		var largeCellFont = LargeCellFontSize;
		var representative = new string('0', StringSampleCap);
		var sampleProperty = TestPropertyTypeDefinitionBuilder.CreateString("sample", StringSampleCap);

		var comboColumn = new GridColumnDefinition(
			Key: "value",
			ColumnType: ColumnTypes.ActionComboBox,
			UiName: "X",
			PropertyTypeId: "sample",
			ReadOnly: false,
			SaveToCsv: true);

		var actions = new Dictionary<int, ActionDefinition>
		{
			[1] = new ActionDefinition(1, representative, DeployDuration.Immediate,
				new[] { new ActionPropertyDefinition("value", null, "sample", null) })
		};

		var registry = TestRecipeMetadataRegistryFactory.Build(
			new[] { sampleProperty }, actions,
			columns: new Dictionary<string, GridColumnDefinition> { ["combo"] = comboColumn });

		var smallFontCalculator = new ColumnWidthCalculator(registry, GridStyleOptions.Default);
		var largeFontCalculator = new ColumnWidthCalculator(
			registry, GridStyleOptions.Default with { Fonts = GridStyleOptions.Default.Fonts with { CellFontSize = largeCellFont, HeaderFontSize = LargeHeaderFontSize } });

		var smallComboWidth = GetPixelWidth(smallFontCalculator.CalculateColumnWidth(comboColumn));
		var largeComboWidth = GetPixelWidth(largeFontCalculator.CalculateColumnWidth(comboColumn));

		var smallContent = MeasureText(representative, smallCellFont, FontWeight.Normal);
		var largeContent = MeasureText(representative, largeCellFont, FontWeight.Normal);

		var smallComboChrome = smallComboWidth - smallContent;
		var largeComboChrome = largeComboWidth - largeContent;

		largeComboChrome.Should().BeApproximately(smallComboChrome, 1.0,
			"the combo chevron budget is a fixed theme DIP (ComboBoxChromeWidth), so it does not scale with the font "
			+ "even as the content grows");
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

		return MeasureText(representative, GridStyleOptions.Default.Fonts.CellFontSize, FontWeight.Normal);
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
			var wordWidth = MeasureText(word, GridStyleOptions.Default.Fonts.HeaderFontSize, FontWeight.Bold);
			if (wordWidth > maxWordWidth)
			{
				maxWordWidth = wordWidth;
			}
		}

		return maxWordWidth + HeaderFloorChrome;
	}

	private double MeasureActionContentWidth()
	{
		var maxWidth = 0.0;
		foreach (var action in _fixture.RecipeMetadataRegistry.GetAllActions())
		{
			var width = MeasureText(action.UiName, GridStyleOptions.Default.Fonts.CellFontSize, FontWeight.Normal);
			if (width > maxWidth)
			{
				maxWidth = width;
			}
		}

		return maxWidth;
	}

	private static double MeasureText(string text, double fontSize, FontWeight fontWeight)
	{
		var typeface = new Typeface(GridFonts.DefaultFamily, FontStyle.Normal, fontWeight);
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
