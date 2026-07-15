using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Stateless OneWay display converter for the transposed recyclable text editor: (Value, FormatKind)
// -> the units-less formatted string the per-cell PropertyTimeEditingConverter used to bake. Binding
// FormatKind instead of baking it is what lets the TextBox be reused across recycled cells; the edit
// commit is owned by the editor's LostFocus/KeyDown handlers (Avalonia MultiBinding has no ConvertBack).
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
