using System.Globalization;

using Avalonia.Data;

using FluentAssertions;

using SemiStep.Core.Recipes;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class CellStateToBoolConverterTests
{
	private readonly CellStateToBoolConverter _converter = new();

	[Theory]
	[InlineData(CellState.Enabled, true)]
	[InlineData(CellState.Readonly, false)]
	[InlineData(CellState.Disabled, false)]
	public void Convert_CellState_ReturnsExpectedBool(CellState input, bool expected)
	{
		var result = _converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(expected);
	}

	[Fact]
	public void Convert_NullValue_ReturnsFalse()
	{
		var result = _converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_NonCellStateValue_ReturnsFalse()
	{
		var result = _converter.Convert("not a CellState", typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void ConvertBack_ReturnsBindingOperationsDoNothing()
	{
		var result = _converter.ConvertBack(true, typeof(CellState), null, CultureInfo.InvariantCulture);

		result.Should().Be(BindingOperations.DoNothing);
	}
}
