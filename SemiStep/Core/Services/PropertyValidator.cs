using Core.Entities;
using Core.Metadata;
using Core.Validation;

namespace Core.Services;

public sealed class PropertyValidator
{
	private readonly IMetadataProvider _metadata;

	public PropertyValidator(IMetadataProvider metadata)
	{
		_metadata = metadata;
	}

	public ValidationResult Validate(ColumnId column, object value)
	{
		if (!_metadata.PropertyExists(column))
			return ValidationResult.Fail(new UnknownPropertyError(column));

		var meta = _metadata.GetProperty(column);
		var errors = new List<ValidationError>();

		ValidateType(meta, value, errors);
		ValidateRange(meta, value, errors);
		ValidateStringLength(meta, value, errors);

		return new ValidationResult(errors);
	}

	private static void ValidateType(PropertyMetadata meta, object value, List<ValidationError> errors)
	{
		var expectedType = GetClrType(meta.Type);
		if (value.GetType() != expectedType)
			errors.Add(new TypeMismatchError(meta.ColumnId, expectedType, value.GetType()));
	}

	private static void ValidateRange(PropertyMetadata meta, object value, List<ValidationError> errors)
	{
		if (value is not IComparable comparable)
			return;

		if (meta.MinValue != null && comparable.CompareTo(meta.MinValue) < 0)
			errors.Add(new ValueTooLowError(meta.ColumnId, meta.MinValue, value));

		if (meta.MaxValue != null && comparable.CompareTo(meta.MaxValue) > 0)
			errors.Add(new ValueTooHighError(meta.ColumnId, meta.MaxValue, value));
	}

	private static void ValidateStringLength(PropertyMetadata meta, object value, List<ValidationError> errors)
	{
		if (value is string str && meta.MaxLength.HasValue && str.Length > meta.MaxLength.Value)
			errors.Add(new StringTooLongError(meta.ColumnId, meta.MaxLength.Value, str.Length));
	}

	private static Type GetClrType(PropertyType type) => type switch
	{
		PropertyType.Int => typeof(int),
		PropertyType.Float => typeof(float),
		PropertyType.String => typeof(string),
		PropertyType.Bool => typeof(bool),
		_ => throw new ArgumentOutOfRangeException(nameof(type))
	};
}
