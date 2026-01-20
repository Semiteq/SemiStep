using SemiStep.Config.Models;

namespace SemiStep.Config.Validation;

public sealed class ValidationResult
{
	public List<ConfigError> Errors { get; } = new();

	public bool IsValid => Errors.All(e => e.Severity != ErrorSeverity.Error);

	public void AddError(string message, string? location = null)
	{
		Errors.Add(new ConfigError(ErrorSeverity.Error, message, location));
	}

	public void AddWarning(string message, string? location = null)
	{
		Errors.Add(new ConfigError(ErrorSeverity.Warning, message, location));
	}

	public void Merge(ValidationResult other)
	{
		Errors.AddRange(other.Errors);
	}
}
