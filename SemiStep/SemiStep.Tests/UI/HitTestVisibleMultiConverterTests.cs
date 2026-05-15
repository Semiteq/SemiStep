using System.Globalization;

using Avalonia;

using FluentAssertions;

using SemiStep.Core.Recipes;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class HitTestVisibleMultiConverterTests
{
	private readonly HitTestVisibleMultiConverter _converter = new();
	private readonly CellStateToBoolConverter _cellStateToBoolConverter = new();

	[Fact]
	public void Convert_CellEnabled_GridNotReadOnly_ReturnsTrue()
	{
		var result = _converter.Convert(new object?[] { true, false }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(true);
	}

	[Fact]
	public void Convert_CellEnabled_GridReadOnly_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { true, true }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_CellDisabled_GridNotReadOnly_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { false, false }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_CellDisabled_GridReadOnly_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { false, true }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_FewerThanTwoSources_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { true }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_UnsetSourceZero_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { AvaloniaProperty.UnsetValue, false }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_UnsetSourceOne_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { true, AvaloniaProperty.UnsetValue }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_NullSourceZero_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { null, false }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_NullSourceOne_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { true, null }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_NonBoolSourceOne_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { true, "not-a-bool" }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Fact]
	public void Convert_NonBoolSourceZero_ReturnsFalse()
	{
		var result = _converter.Convert(new object?[] { "not-a-bool", false }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(false);
	}

	[Theory]
	[InlineData(CellState.Enabled, false, true)]
	[InlineData(CellState.Enabled, true, false)]
	[InlineData(CellState.Disabled, false, false)]
	[InlineData(CellState.Disabled, true, false)]
	[InlineData(CellState.Readonly, false, false)]
	[InlineData(CellState.Readonly, true, false)]
	public void Convert_FullPipeline_CellStateAndGridReadOnly_ProducesExpectedResult(
		CellState cellState,
		bool gridReadOnly,
		bool expected)
	{
		var cellEnabled = _cellStateToBoolConverter.Convert(cellState, typeof(bool), null, CultureInfo.InvariantCulture);

		var result = _converter.Convert(new object?[] { cellEnabled, gridReadOnly }, typeof(bool), null, CultureInfo.InvariantCulture);

		result.Should().Be(expected);
	}
}
