using System.Globalization;

using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

public sealed class DictionaryEntryConverter<TValue>(string key, TValue defaultValue) : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is IReadOnlyDictionary<string, TValue> dictionary
			&& dictionary.TryGetValue(key, out var entry))
		{
			return entry;
		}

		return defaultValue;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value;
	}
}
