using System.ComponentModel.DataAnnotations;

namespace Config.Validation.Attributes;

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

	protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
	{
		if (value == null)
			return ValidationResult.Success;

		if (value is not IConvertible convertible)
			return new ValidationResult($"Value must be a numeric type");

		try
		{
			var numericValue = Convert.ToDouble(convertible);

			if (numericValue < Minimum || numericValue > Maximum)
			{
				return new ValidationResult(
					$"Value {numericValue} is out of range [{Minimum}, {Maximum}]");
			}

			return ValidationResult.Success;
		}
		catch
		{
			return new ValidationResult($"Unable to convert value to numeric type");
		}
	}
}
