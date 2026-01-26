using System.Globalization;

using Avalonia.Data.Converters;

using Recipe.Entities;

namespace UI.Converters;

public class PropertyValueConverter : IValueConverter
{
	private readonly string _propertyTypeId;

	public PropertyValueConverter(string propertyTypeId)
	{
		_propertyTypeId = propertyTypeId;
	}

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is ViewModels.RecipeRowViewModel row)
		{
			return row.GetPropertyValue(_propertyTypeId);
		}

		return null;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value;
	}
}
