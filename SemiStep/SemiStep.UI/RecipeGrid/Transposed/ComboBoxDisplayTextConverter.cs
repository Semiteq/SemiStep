using System.Globalization;

using Avalonia.Data.Converters;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

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
