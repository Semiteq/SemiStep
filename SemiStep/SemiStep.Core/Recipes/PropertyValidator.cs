using FluentResults;

namespace SemiStep.Core.Recipes;

internal static class PropertyValidator
{
	internal static Result Validate(PropertyTypeDefinition property, object value)
	{
		return property.SystemType.ToLowerInvariant() switch
		{
			"int" => value is int intVal
				? ValidateNumericRange(property, (double)intVal)
				: Result.Fail($"Expected int value but got {value.GetType().Name} for '{property.Id}'"),
			"float" => value is float floatVal
				? ValidateNumericRange(property, (double)floatVal)
				: Result.Fail($"Expected float value but got {value.GetType().Name} for '{property.Id}'"),
			"string" => ValidateStringLength(property, value),
			_ => Result.Fail($"Unsupported property system type: {property.SystemType}")
		};
	}

	internal static Result ValidateGroupValue(
		ActionPropertyDefinition actionProperty,
		PropertyValue parsed,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		if (actionProperty.GroupName is null)
		{
			return Result.Ok();
		}

		if (parsed.Value is not int intKey)
		{
			return Result.Fail($"Group value must be integer, got {parsed.Type}");
		}

		return recipeMetadataRegistry.GroupHasIntKey(intKey, actionProperty.GroupName);
	}

	private static Result ValidateNumericRange(PropertyTypeDefinition property, double value)
	{
		if (property.Min.HasValue && value < property.Min.Value)
		{
			return Result.Fail(
				$"Value {value} is below minimum {property.Min.Value} for '{property.Id}'");
		}

		if (property.Max.HasValue && value > property.Max.Value)
		{
			return Result.Fail(
				$"Value {value} exceeds maximum {property.Max.Value} for '{property.Id}'");
		}

		return Result.Ok();
	}

	private static Result ValidateStringLength(PropertyTypeDefinition property, object value)
	{
		if (value is string str && property.MaxLength.HasValue && str.Length > property.MaxLength.Value)
		{
			return Result.Fail(
				$"String length {str.Length} exceeds maximum {property.MaxLength.Value} for '{property.Id}'");
		}

		return Result.Ok();
	}
}
