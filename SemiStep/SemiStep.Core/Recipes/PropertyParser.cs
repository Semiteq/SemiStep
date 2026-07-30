using System;
using System.Globalization;

using FluentResults;

using SemiStep.Core.Recipes.Errors;

namespace SemiStep.Core.Recipes;

public static class PropertyParser
{
	public static Result<PropertyValue> Parse(string input, PropertyTypeDefinition propertyDefinition)
	{
		var propertyType = PropertyTypeMapping.FromSystemType(propertyDefinition.SystemType);

		return propertyType switch
		{
			PropertyType.Int => ParseInt(input),
			PropertyType.Float => ParseFloat(input),
			PropertyType.String => Result.Ok(PropertyValue.FromString(input)),
			_ => throw new InvalidOperationException(
				$"Unknown property type '{propertyType}'. FromSystemType only yields Int/Float/String, so this arm is unreachable.")
		};
	}

	private static Result<PropertyValue> ParseInt(string rawValue)
	{
		if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return Result.Ok(PropertyValue.FromInt(result));
		}

		return Result.Fail(new PropertyValueParseError(rawValue, "integer"));
	}

	private static Result<PropertyValue> ParseFloat(string rawValue)
	{
		if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return Result.Ok(PropertyValue.FromFloat(result));
		}

		return Result.Fail(new PropertyValueParseError(rawValue, "float"));
	}
}
