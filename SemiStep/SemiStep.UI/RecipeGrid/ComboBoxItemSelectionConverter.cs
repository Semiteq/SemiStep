using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

internal sealed class ComboBoxItemSelectionConverter(IReadOnlyList<ComboBoxItemViewModel> items) : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not int id)
		{
			return null;
		}

		return items.FirstOrDefault(item => item.Id == id);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is ComboBoxItemViewModel selectedItem)
		{
			return selectedItem.Id;
		}

		return BindingOperations.DoNothing;
	}
}
