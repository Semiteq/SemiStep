using Core.Properties.Contracts;

using FluentResults;

using OneOf;

namespace Core.Properties;

internal sealed record Property
{
	private OneOf<short, float, string> InternalUnionValue { get; init; }
	private IPropertyDefinition Definition { get; init; }
	internal string DisplayValue => $"{Definition.FormatValue(InternalUnionValue.Value)} {Definition.Units}".Trim();

	private Property(OneOf<short, float, string> value, IPropertyDefinition definition)
	{
		InternalUnionValue = value;
		Definition = definition;
	}

	internal static Result<Property> Create(object typedValue, IPropertyDefinition definition)
	{
		if (typedValue.GetType() != definition.SystemType)
		{
			return PropertyError.TypeMismatch
				(
					expectedType: typedValue.GetType().Name,
					actualType: definition.SystemType.Name
				);
		}

		var unionResult = definition.GetValidatedValue(typedValue)
			.Bind(definition.GetValueSignRespected)
			.Bind(ConvertObjectToUnion);
		return new Property(unionResult.Value, definition);
	}

	private static Result<OneOf<short, float, string>> ConvertObjectToUnion(object value) =>
		value switch
		{
			short s => Result.Ok<OneOf<short, float, string>>(s),
			float f => Result.Ok<OneOf<short, float, string>>(f),
			string str => Result.Ok<OneOf<short, float, string>>(str),

			_ => PropertyError.ConversionFailed
				(
					from: value.GetType().Name,
					to: "Union<short, float, string>"
				)
		};
}
