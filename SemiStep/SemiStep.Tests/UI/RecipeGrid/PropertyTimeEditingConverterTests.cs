using System.Globalization;

using Avalonia.Data;

using FluentAssertions;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

/// <summary>
/// Exercises <see cref="PropertyTimeEditingConverter.ConvertBack"/> around empty input. A cleared
/// string cell must commit an empty value; a cleared numeric/time cell must leave the source
/// untouched so an unparseable empty never overwrites the prior value.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class PropertyTimeEditingConverterTests
{
	[Fact]
	public void ConvertBack_EmptyText_StringColumn_CommitsEmptyString()
	{
		var converter = new PropertyTimeEditingConverter(TimeFormatHelper.DefaultFormatKind, allowsEmpty: true);

		var result = ConvertBack(converter, string.Empty);

		result.Should().Be(string.Empty);
	}

	[Fact]
	public void ConvertBack_WhitespaceText_StringColumn_CommitsEmptyString()
	{
		var converter = new PropertyTimeEditingConverter(TimeFormatHelper.DefaultFormatKind, allowsEmpty: true);

		var result = ConvertBack(converter, "   ");

		result.Should().Be(string.Empty);
	}

	[Fact]
	public void ConvertBack_EmptyText_NumericColumn_LeavesSourceUntouched()
	{
		var converter = new PropertyTimeEditingConverter(TimeFormatHelper.DefaultFormatKind, allowsEmpty: false);

		var result = ConvertBack(converter, string.Empty);

		result.Should().Be(BindingOperations.DoNothing);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("59")]
	[InlineData("3600")]
	[InlineData("86399")]
	[InlineData("86400")]
	public void ConvertBack_FormattedTimeValue_RoundTripsToSameSeconds(string seconds)
	{
		var converter = new PropertyTimeEditingConverter(TimeFormatHelper.TimeHmsFormat, allowsEmpty: false);

		var displayed = converter.Convert(seconds, typeof(string), parameter: null, CultureInfo.InvariantCulture);
		var result = ConvertBack(converter, displayed as string);

		result.Should().Be(seconds);
	}

	[Fact]
	public void ConvertBack_MinutesOverSixty_IsRejected()
	{
		var converter = new PropertyTimeEditingConverter(TimeFormatHelper.TimeHmsFormat, allowsEmpty: false);

		var result = ConvertBack(converter, "00:61:00");

		result.Should().Be(BindingOperations.DoNothing);
	}

	[Theory]
	[InlineData("12:30")]
	[InlineData("1:30")]
	public void ConvertBack_ClockLikeText_NonTimeColumn_IsKeptAsTyped(string text)
	{
		var nonTimeColumn = new PropertyTimeEditingConverter(TimeFormatHelper.DefaultFormatKind, allowsEmpty: true);

		ConvertBack(nonTimeColumn, text).Should().Be(text);
	}

	[Fact]
	public void ConvertBack_ClockLikeText_TimeColumn_ParsesToSeconds()
	{
		var converter = new PropertyTimeEditingConverter(TimeFormatHelper.TimeHmsFormat, allowsEmpty: false);

		ConvertBack(converter, "12:30").Should().Be("45000");
	}

	private static object? ConvertBack(PropertyTimeEditingConverter converter, string? value)
	{
		return converter.ConvertBack(value, typeof(object), parameter: null, CultureInfo.InvariantCulture);
	}
}
