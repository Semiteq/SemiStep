using Core.Properties.Contracts;
using Core.Reasons.Errors;

namespace Core.Properties;

public sealed record Property
{
	private OneOf<short, float, string> InternalUnionValue { get; init; }
	private IPropertyTypeDefinition PropertyDefinition { get; init; }

	public object GetValueAsObject => InternalUnionValue.Match<object>(
		shortValue => shortValue,
		floatValue => floatValue,
		stringValue => stringValue
	);

	public TResult Match<TResult>(
		Func<short, TResult> onShort,
		Func<float, TResult> onFloat,
		Func<string, TResult> onString)
		=> InternalUnionValue.Match(onShort, onFloat, onString);

	public string GetDisplayValue =>
		$"{PropertyDefinition.FormatValue(InternalUnionValue.Value)} {PropertyDefinition.Units}".Trim();

	private Property(OneOf<short, float, string> value, IPropertyTypeDefinition propertyDefinition)
	{
		InternalUnionValue = value;
		PropertyDefinition = propertyDefinition;
	}

	public static Result<Property> Create(object value, IPropertyTypeDefinition propertyDefinition)
	{
		if (value.GetType() != propertyDefinition.SystemType)
			return new CorePropertyTypeMismatchError(value.GetType().ToString(), propertyDefinition.SystemType.Name);

		var validationResult = propertyDefinition.TryValidate(value);
		if (validationResult.IsFailed)
			return validationResult;

		var actualValue = ApplyNonNegativeIfNeeded(value, propertyDefinition);
		if (actualValue.IsFailed)
			return actualValue.ToResult();

		var conversionResult = ConvertObjectToUnion(actualValue.Value);
		if (conversionResult.IsFailed)
			return conversionResult.ToResult();

		return new Property(conversionResult.Value, propertyDefinition);
	}

	public Result<Property> WithValue(object newValue)
	{
		var parseResult = PropertyDefinition.TryParse(newValue.ToString());
		if (parseResult.IsFailed)
			return parseResult.ToResult();

		var validationResult = PropertyDefinition.TryValidate(parseResult.Value);
		if (validationResult.IsFailed)
			return validationResult;

		var actualValue = ApplyNonNegativeIfNeeded(parseResult.Value, PropertyDefinition);
		if (actualValue.IsFailed)
			return actualValue.ToResult();

		var conversionResult = ConvertObjectToUnion(actualValue.Value);
		if (conversionResult.IsFailed)
			return conversionResult.ToResult();

		return Result.Ok(new Property(conversionResult.Value, PropertyDefinition));
	}

	public Result<T> GetValue<T>() where T : notnull
	{
		return InternalUnionValue.Match<Result<T>>(
			shortValue =>
			{
				if (shortValue is T typedValue)
					return Result.Ok(typedValue);

				return new CorePropertyTypeMismatchError(typeof(T).ToString(), "short");
			},
			floatValue =>
			{
				if (floatValue is T typedValue)
					return Result.Ok(typedValue);

				return new CorePropertyTypeMismatchError(typeof(T).ToString(), "float");
			},
			stringValue =>
			{
				if (stringValue is T typedValue)
					return Result.Ok(typedValue);

				return new CorePropertyTypeMismatchError(typeof(T).ToString(), "string");
			}
		);
	}

	public Result<double> GetNumeric()
	{
		return InternalUnionValue.Match<Result<double>>(
			shortValue => (double)shortValue,
			floatValue => (double)floatValue,
			stringValue => new CorePropertyNonNumericError()
		);
	}

	private static Result<object> ApplyNonNegativeIfNeeded(object value, IPropertyTypeDefinition propertyDefinition)
	{
		if (!propertyDefinition.NonNegative)
			return Result.Ok(value);

		return propertyDefinition.GetNonNegativeValue(value);
	}

	private static Result<OneOf<short, float, string>> ConvertObjectToUnion(object value) =>
		value switch
		{
			short i => Result.Ok<OneOf<short, float, string>>(i),
			float f => Result.Ok<OneOf<short, float, string>>(f),
			string s => Result.Ok<OneOf<short, float, string>>(s),
			_ => new CorePropertyConversionFailedError(value.ToString(), "Union")
		};
}
