using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal sealed class InapplicableColumnsConverter(string columnKey) : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not IReadOnlySet<string> inapplicableColumns)
		{
			return false;
		}

		return inapplicableColumns.Contains(columnKey);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return BindingOperations.DoNothing;
	}
}
