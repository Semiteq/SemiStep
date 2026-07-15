using System.Globalization;

using Avalonia.Data.Converters;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Resolves a combo cell's (Value, Items) to the selected item's display text for the lazy display
// TextBlock. It delegates the (id, items) -> item lookup to ComboBoxItemMultiSelectionConverter (the same
// resolver the ComboBox editor's SelectedItem uses) and projects the item's DisplayText, so the display
// updates on any external Value change (selector edit, action change, recycle rebind) through the same
// OneWay MultiBinding the editor uses, with the lookup living in exactly one place.
internal sealed class ComboBoxDisplayTextConverter : IMultiValueConverter
{
	public static readonly ComboBoxDisplayTextConverter Instance = new();

	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		var selected = ComboBoxItemMultiSelectionConverter.Instance.Convert(
			values, typeof(ComboBoxItemViewModel), parameter, culture);

		return (selected as ComboBoxItemViewModel)?.DisplayText ?? string.Empty;
	}
}
