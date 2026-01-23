using Recipe.Entities;
using Recipe.Validation;

using Shared.Entities;

namespace Recipe.Services;

public sealed class PropertyValidator
{
	public static ValidationResult Validate(PropertyDefinition property, object value)
	{
		var errors = new List<ValidationError>();

		var expectedType = ParsePropertyType(property.SystemType);
		ValidateType(expectedType, value, property.PropertyTypeId, errors);
		ValidateRange(property, value, errors);
		ValidateStringLength(property, value, errors);

		return new ValidationResult(errors);
	}

	private static void ValidateType(
		PropertyType expectedType,
		object value,
		string propertyId,
		List<ValidationError> errors)
	{
		var clrType = GetClrType(expectedType);
		if (value.GetType() != clrType)
		{
			errors.Add(new TypeMismatchError(new ColumnId(propertyId), clrType, value.GetType()));
		}
	}

	private static void ValidateRange(PropertyDefinition property, object value, List<ValidationError> errors)
	{
		if (value is not IComparable comparable)
		{
			return;
		}

		if (property.Min.HasValue && comparable.CompareTo(property.Min.Value) < 0)
		{
			errors.Add(new ValueTooLowError(new ColumnId(property.PropertyTypeId), property.Min.Value, value));
		}

		if (property.Max.HasValue && comparable.CompareTo(property.Max.Value) > 0)
		{
			errors.Add(new ValueTooHighError(new ColumnId(property.PropertyTypeId), property.Max.Value, value));
		}
	}

	private static void ValidateStringLength(PropertyDefinition property, object value, List<ValidationError> errors)
	{
		if (value is string str && property.MaxLength.HasValue && str.Length > property.MaxLength.Value)
		{
			errors.Add(new StringTooLongError(new ColumnId(property.PropertyTypeId), property.MaxLength.Value, str.Length));
		}
	}

	private static PropertyType ParsePropertyType(string systemType)
	{
		return systemType.ToLowerInvariant() switch
		{
			"int" or "int32" or "integer" => PropertyType.Int,
			"float" or "single" or "double" => PropertyType.Float,
			"string" or "text" => PropertyType.String,
			_ => PropertyType.String
		};
	}

	private static Type GetClrType(PropertyType type) => type switch
	{
		PropertyType.Int => typeof(int),
		PropertyType.Float => typeof(float),
		PropertyType.String => typeof(string),
		_ => throw new ArgumentOutOfRangeException(nameof(type))
	};
}
