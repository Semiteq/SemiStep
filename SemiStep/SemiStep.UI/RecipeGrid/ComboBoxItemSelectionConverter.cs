using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal sealed class ComboBoxItemSelectionConverter(IReadOnlyList<ComboBoxItemViewModel> items) : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is not int id ? null : (object?)items.FirstOrDefault(item => item.Id == id);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is ComboBoxItemViewModel selectedItem ? selectedItem.Id : BindingOperations.DoNothing;
	}
}
