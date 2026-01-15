using Config.Models;

namespace Config.Validation;

/// <summary>
/// Композитный валидатор для конфигурации
/// Объединяет несколько валидаторов и выполняет их последовательно
/// </summary>
public sealed class ConfigSchemaValidator : IValidator
{
	private readonly List<IValidator> _validators;

	public ConfigSchemaValidator(params IValidator[] validators)
	{
		_validators = validators?.ToList() ?? new List<IValidator>();
	}

	public async Task<ValidationResult> ValidateAsync(object config, ConfigContext context)
	{
		var result = new ValidationResult();

		foreach (var validator in _validators)
		{
			var validationResult = await validator.ValidateAsync(config, context);
			result.Merge(validationResult);
		}

		return result;
	}

	/// <summary>
	/// Добавляет валидатор в цепочку
	/// </summary>
	public void AddValidator(IValidator validator)
	{
		if (validator == null)
			throw new ArgumentNullException(nameof(validator));

		_validators.Add(validator);
	}
}
