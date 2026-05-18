using System.Globalization;

using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// OneWay-only resolver from (Id, items) to ComboBoxItemViewModel. Writeback is owned by
/// SelectionChanged in ComboBoxCellFactory; ConvertBack returns DoNothing.
/// </summary>
internal sealed class ComboBoxItemMultiSelectionConverter : IMultiValueConverter
{
	public static readonly ComboBoxItemMultiSelectionConverter Instance = new();

	private static readonly object?[] _doNothingResult = [BindingOperations.DoNothing];

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

		if (itemsValue is not IEnumerable<ComboBoxItemViewModel> items)
		{
			return null;
		}

		return items.FirstOrDefault(item => item.Id == id);
	}

	public object?[] ConvertBack(object? value, IList<Type> targetTypes, object? parameter, CultureInfo culture)
	{
		return _doNothingResult;
	}
}
