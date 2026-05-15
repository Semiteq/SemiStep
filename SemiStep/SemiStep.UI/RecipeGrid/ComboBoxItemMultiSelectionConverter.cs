using System.Globalization;

using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

public sealed class ComboBoxItemMultiSelectionConverter : IMultiValueConverter
{
	private static readonly object?[] _noOpConvertBack =
		new object?[] { BindingOperations.DoNothing, BindingOperations.DoNothing };

	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count < 2)
		{
			return null;
		}

		var idValue = values[0];
		var itemsValue = values[1];

		if (idValue == AvaloniaProperty.UnsetValue || itemsValue == AvaloniaProperty.UnsetValue)
		{
			return null;
		}

		if (idValue is not int id)
		{
			return null;
		}

		if (itemsValue is not IReadOnlyList<ComboBoxItemViewModel> items)
		{
			return null;
		}

		return items.FirstOrDefault(item => item.Id == id);
	}

	public object?[] ConvertBack(object? value, IList<Type> targetTypes, object? parameter, CultureInfo culture)
	{
		if (value is ComboBoxItemViewModel selectedItem)
		{
			return new object?[] { selectedItem.Id, BindingOperations.DoNothing };
		}

		return _noOpConvertBack;
	}
}
