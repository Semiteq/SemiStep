using System.Globalization;

using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

public sealed class HitTestVisibleMultiConverter : IMultiValueConverter
{
	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count < 2)
		{
			return false;
		}

		if (values[0] is not bool cellEnabled || values[1] is not bool gridReadOnly)
		{
			return false;
		}

		return cellEnabled && !gridReadOnly;
	}
}
