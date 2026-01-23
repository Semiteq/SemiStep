using Recipe.Validation;

namespace Recipe.Exceptions;

public sealed class PropertyValidationException(ValidationResult validationResult)
	: RecipeException(FormatMessage(validationResult))
{
	private static string FormatMessage(ValidationResult result)
	{
		if (result.IsValid)
		{
			return "No validation errors";
		}

		var errorMessages = result.Errors.Select(e => $"[{e.Column}] {e.Message}");
		return $"Validation failed: {string.Join("; ", errorMessages)}";
	}
}
