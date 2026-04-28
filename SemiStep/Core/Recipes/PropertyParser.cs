using System.Globalization;

using FluentResults;

namespace SemiStep.Core.Recipes;

public sealed class PropertyParser
{
	public Result<PropertyValue> Parse(string input, PropertyTypeDefinition propertyDefinition)
	{
		var propertyType = PropertyTypeMapping.FromSystemType(propertyDefinition.SystemType);

		return propertyType switch
		{
			PropertyType.Int => ParseInt(input),
			PropertyType.Float => ParseFloat(input),
			PropertyType.String => Result.Ok(PropertyValue.FromString(input)),
			_ => Result.Fail($"Unknown property type '{propertyType}'")
		};
	}

	private static Result<PropertyValue> ParseInt(string rawValue)
	{
		if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return Result.Ok(PropertyValue.FromInt(result));
		}

		return Result.Fail($"Cannot parse '{rawValue}' as integer");
	}

	private static Result<PropertyValue> ParseFloat(string rawValue)
	{
		if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return Result.Ok(PropertyValue.FromFloat(result));
		}

		return Result.Fail($"Cannot parse '{rawValue}' as float");
	}
}
