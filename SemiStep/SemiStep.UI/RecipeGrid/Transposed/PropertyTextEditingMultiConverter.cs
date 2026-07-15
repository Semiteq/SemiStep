using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class PropertyTextEditingMultiConverter : IMultiValueConverter
{
	public static readonly PropertyTextEditingMultiConverter Instance = new();

	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count < 2
			|| values[0] == AvaloniaProperty.UnsetValue
			|| values[1] == AvaloniaProperty.UnsetValue)
		{
			return string.Empty;
		}

		return PropertyTimeEditingConverter.FormatForDisplay(values[0], values[1] as string);
	}
}
