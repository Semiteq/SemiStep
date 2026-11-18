using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// OneWay-only resolver from (Id, items) to ComboBoxItemViewModel. Writeback is owned by
/// SelectionChanged in ComboBoxCellFactory.
/// </summary>
internal sealed class ComboBoxItemMultiSelectionConverter : IMultiValueConverter
{
	public static readonly ComboBoxItemMultiSelectionConverter Instance = new();

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

		return idValue is not int id
			? null
			: itemsValue is not IEnumerable<ComboBoxItemViewModel> items ? null : (object?)items.FirstOrDefault(item => item.Id == id);
	}
}
