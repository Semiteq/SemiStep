namespace Recipe.Validation;

public sealed class ValidationResult(IEnumerable<ValidationError> errors)
{
	// todo: remake into snapshot
	public IReadOnlyList<ValidationError> Errors { get; } = errors.ToList().AsReadOnly();

	public bool IsValid => Errors.Count == 0;

	public static ValidationResult Success() => new([]);

	public static ValidationResult Fail(ValidationError error) => new([error]);

	public static ValidationResult Fail(IEnumerable<ValidationError> errors) => new(errors);
}
