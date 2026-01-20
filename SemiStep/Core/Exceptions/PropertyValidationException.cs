using Core.Validation;

namespace Core.Exceptions;

public sealed class PropertyValidationException : CoreException
{
	public ValidationResult ValidationResult { get; }

	public PropertyValidationException(ValidationResult validationResult)
		: base(FormatMessage(validationResult))
	{
		ValidationResult = validationResult;
	}

	private static string FormatMessage(ValidationResult result)
	{
		if (result.IsValid)
			return "No validation errors";

		var errorMessages = result.Errors.Select(e => $"[{e.Column}] {e.Message}");
		return $"Validation failed: {string.Join("; ", errorMessages)}";
	}
}
