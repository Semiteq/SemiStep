using System.ComponentModel.DataAnnotations;

using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace SemiStep.Config.Validation.Attributes;

/// <summary>
/// Атрибут для валидации числовых диапазонов
/// Использ используется для проверки, что значение находится в заданном диапазоне
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class RangeValidationAttribute : ValidationAttribute
{
	public double Minimum { get; }
	public double Maximum { get; }

	public RangeValidationAttribute(double minimum, double maximum)
	{
		Minimum = minimum;
		Maximum = maximum;
	}

	protected override DataAnnotationsValidationResult? IsValid(object? value, ValidationContext validationContext)
	{
		if (value == null)
			return DataAnnotationsValidationResult.Success;

		if (value is not IConvertible convertible)
			return new DataAnnotationsValidationResult($"Value must be a numeric type");

		try
		{
			var numericValue = Convert.ToDouble(convertible);

			if (numericValue < Minimum || numericValue > Maximum)
			{
				return new DataAnnotationsValidationResult(
					$"Value {numericValue} is out of range [{Minimum}, {Maximum}]");
			}

			return DataAnnotationsValidationResult.Success;
		}
		catch
		{
			return new DataAnnotationsValidationResult($"Unable to convert value to numeric type");
		}
	}
}
