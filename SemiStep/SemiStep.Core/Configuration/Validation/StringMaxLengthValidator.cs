using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Validation;

/// <summary>All string properties share one positive max_length: it sizes the PLC string array element.</summary>
internal static class StringMaxLengthValidator
{
	public static Result Validate(List<PropertyDto> properties)
	{
		var stringProperties = properties
			.Where(property => string.Equals(property.SystemType, "string", StringComparison.OrdinalIgnoreCase))
			.ToList();

		if (stringProperties.Count == 0)
		{
			return Result.Fail(new NoStringPropertyError());
		}

		var errors = new List<IError>();

		foreach (var property in stringProperties)
		{
			if (property.MaxLength is not { } maxLength)
			{
				errors.Add(new StringMaxLengthMissingError(property.PropertyTypeId!));
			}
			else if (maxLength <= 0)
			{
				errors.Add(new StringMaxLengthNotPositiveError(property.PropertyTypeId!, maxLength));
			}
		}

		var distinctPositiveMaxLengths = stringProperties
			.Select(property => property.MaxLength)
			.OfType<int>()
			.Where(maxLength => maxLength > 0)
			.Distinct()
			.ToList();

		if (distinctPositiveMaxLengths.Count > 1)
		{
			errors.Add(new StringMaxLengthConflictError(distinctPositiveMaxLengths));
		}

		return errors.Count == 0 ? Result.Ok() : Result.Fail(errors);
	}
}
