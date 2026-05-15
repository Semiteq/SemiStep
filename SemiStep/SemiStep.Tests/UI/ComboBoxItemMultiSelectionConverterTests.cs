using System.Globalization;

using Avalonia;
using Avalonia.Data;

using FluentAssertions;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class ComboBoxItemMultiSelectionConverterTests
{
	private readonly ComboBoxItemMultiSelectionConverter _converter = new();

	private static IReadOnlyList<ComboBoxItemViewModel> BuildItems()
	{
		return new List<ComboBoxItemViewModel>
		{
			new(1, "One"),
			new(7, "Seven"),
			new(42, "Forty-Two")
		};
	}

	[Fact]
	public void Convert_KnownId_ReturnsMatchingItem()
	{
		var items = BuildItems();

		var result = _converter.Convert(
			new object?[] { 7, items },
			typeof(ComboBoxItemViewModel),
			null,
			CultureInfo.InvariantCulture);

		result.Should().BeOfType<ComboBoxItemViewModel>()
			.Which.Id.Should().Be(7);
	}

	[Fact]
	public void Convert_UnknownId_ReturnsNull()
	{
		var items = BuildItems();

		var result = _converter.Convert(
			new object?[] { 99, items },
			typeof(ComboBoxItemViewModel),
			null,
			CultureInfo.InvariantCulture);

		result.Should().BeNull();
	}

	[Fact]
	public void Convert_NullIdValue_ReturnsNull()
	{
		var items = BuildItems();

		var result = _converter.Convert(
			new object?[] { null, items },
			typeof(ComboBoxItemViewModel),
			null,
			CultureInfo.InvariantCulture);

		result.Should().BeNull();
	}

	[Fact]
	public void Convert_NullItemsValue_ReturnsNull()
	{
		var result = _converter.Convert(
			new object?[] { 7, null },
			typeof(ComboBoxItemViewModel),
			null,
			CultureInfo.InvariantCulture);

		result.Should().BeNull();
	}

	[Fact]
	public void Convert_NonIntFirstSource_ReturnsNull()
	{
		var items = BuildItems();

		var result = _converter.Convert(
			new object?[] { "not-an-int", items },
			typeof(ComboBoxItemViewModel),
			null,
			CultureInfo.InvariantCulture);

		result.Should().BeNull();
	}

	[Fact]
	public void Convert_UnsetIdValue_ReturnsNull()
	{
		var items = BuildItems();

		var result = _converter.Convert(
			new object?[] { AvaloniaProperty.UnsetValue, items },
			typeof(ComboBoxItemViewModel),
			null,
			CultureInfo.InvariantCulture);

		result.Should().BeNull();
	}

	[Fact]
	public void Convert_UnsetItemsValue_ReturnsNull()
	{
		var result = _converter.Convert(
			new object?[] { 7, AvaloniaProperty.UnsetValue },
			typeof(ComboBoxItemViewModel),
			null,
			CultureInfo.InvariantCulture);

		result.Should().BeNull();
	}

	[Fact]
	public void Convert_FewerThanTwoSources_ReturnsNull()
	{
		var result = _converter.Convert(
			new object?[] { 7 },
			typeof(ComboBoxItemViewModel),
			null,
			CultureInfo.InvariantCulture);

		result.Should().BeNull();
	}

	[Fact]
	public void ConvertBack_ComboBoxItem_WritesIdToFirstSourceAndDoNothingToSecond()
	{
		var item = new ComboBoxItemViewModel(7, "Seven");

		var result = _converter.ConvertBack(
			item,
			new[] { typeof(int), typeof(IReadOnlyList<ComboBoxItemViewModel>) },
			null,
			CultureInfo.InvariantCulture);

		result.Should().HaveCount(2);
		result[0].Should().Be(7);
		result[1].Should().Be(BindingOperations.DoNothing);
	}

	[Fact]
	public void ConvertBack_NullValue_ReturnsDoNothingForBothSources()
	{
		var result = _converter.ConvertBack(
			null,
			new[] { typeof(int), typeof(IReadOnlyList<ComboBoxItemViewModel>) },
			null,
			CultureInfo.InvariantCulture);

		result.Should().HaveCount(2);
		result[0].Should().Be(BindingOperations.DoNothing);
		result[1].Should().Be(BindingOperations.DoNothing);
	}

	[Fact]
	public void ConvertBack_NonComboBoxItemValue_ReturnsDoNothingForBothSources()
	{
		var result = _converter.ConvertBack(
			"not-a-combobox-item",
			new[] { typeof(int), typeof(IReadOnlyList<ComboBoxItemViewModel>) },
			null,
			CultureInfo.InvariantCulture);

		result.Should().HaveCount(2);
		result[0].Should().Be(BindingOperations.DoNothing);
		result[1].Should().Be(BindingOperations.DoNothing);
	}

	[Fact]
	public void RoundTrip_SourceIdToVmAndBackToId_PreservesValue()
	{
		var items = BuildItems();

		var converted = _converter.Convert(
			new object?[] { 7, items },
			typeof(ComboBoxItemViewModel),
			null,
			CultureInfo.InvariantCulture);

		var back = _converter.ConvertBack(
			converted,
			new[] { typeof(int), typeof(IReadOnlyList<ComboBoxItemViewModel>) },
			null,
			CultureInfo.InvariantCulture);

		back[0].Should().Be(7);
		back[1].Should().Be(BindingOperations.DoNothing);
	}
}
