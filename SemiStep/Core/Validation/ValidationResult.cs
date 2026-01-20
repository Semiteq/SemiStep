namespace Core.Validation;

public sealed class ValidationResult
{
	public IReadOnlyList<ValidationError> Errors { get; }

	public bool IsValid => Errors.Count == 0;

	public ValidationResult(IEnumerable<ValidationError> errors)
	{
		Errors = errors.ToList().AsReadOnly();
	}

	public static ValidationResult Success() => new([]);

	public static ValidationResult Fail(ValidationError error) => new([error]);

	public static ValidationResult Fail(IEnumerable<ValidationError> errors) => new(errors);
}
