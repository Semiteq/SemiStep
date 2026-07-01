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

	private static object? ConvertBack(PropertyTimeEditingConverter converter, string? value)
	{
		return converter.ConvertBack(value, typeof(object), parameter: null, CultureInfo.InvariantCulture);
	}
}
