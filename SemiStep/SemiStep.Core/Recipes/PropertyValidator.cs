using FluentResults;

using SemiStep.Core.Recipes.Errors;

namespace SemiStep.Core.Recipes;

internal static class PropertyValidator
{
	internal static Result Validate(PropertyTypeDefinition property, object value)
	{
		return property.SystemType.ToLowerInvariant() switch
		{
			"int" => value is int intVal
				? ValidateNumericRange(property, intVal)
				: Result.Fail(new PropertyValueTypeMismatchError("int", value.GetType().Name, property.Id)),
			"float" => value is float floatVal
				? ValidateNumericRange(property, (double)floatVal)
				: Result.Fail(new PropertyValueTypeMismatchError("float", value.GetType().Name, property.Id)),
			"string" => ValidateStringLength(property, value),
			_ => Result.Fail(new UnsupportedPropertySystemTypeError(property.SystemType))
		};
	}

	internal static Result ValidateGroupValue(
		ActionPropertyDefinition actionProperty,
		PropertyValue parsed,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		return actionProperty.GroupName is null
			? Result.Ok()
			: parsed.Value is not int intKey
			? Result.Fail(new GroupValueNotIntegerError(parsed.Type))
			: recipeMetadataRegistry.GroupHasIntKey(intKey, actionProperty.GroupName);
	}

	private static Result ValidateNumericRange(PropertyTypeDefinition property, double value)
	{
		return property.Min.HasValue && value < property.Min.Value
			? Result.Fail(
				new ValueBelowMinimumError(value, property.Min.Value, property.Id))
			: property.Max.HasValue && value > property.Max.Value
			? Result.Fail(
				new ValueAboveMaximumError(value, property.Max.Value, property.Id))
			: Result.Ok();
	}

	private static Result ValidateStringLength(PropertyTypeDefinition property, object value)
	{
		if (value is not string str)
		{
			return Result.Fail(
				new PropertyValueTypeMismatchError("string", value.GetType().Name, property.Id));
		}

		return str.Contains('\0')
			? Result.Fail(
				new StringContainsNulError(property.Id))
			: property.MaxLength.HasValue && str.Length > property.MaxLength.Value
			? Result.Fail(
				new StringTooLongError(str.Length, property.MaxLength.Value, property.Id))
			: Result.Ok();
	}
}
