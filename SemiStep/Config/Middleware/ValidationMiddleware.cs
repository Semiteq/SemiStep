using Config.Models;
using Config.Validation;

namespace Config.Middleware;

/// <summary>
/// Middleware для валидации конфигурации
/// Проверяет диапазоны значений, линковку объектов и другие правила
/// </summary>
public sealed class ValidationMiddleware : IConfigMiddleware
{
	private readonly IValidator _validator;

	public string Name => "Validation";

	public ValidationMiddleware(IValidator validator)
	{
		_validator = validator ?? throw new ArgumentNullException(nameof(validator));
	}

	public async Task ExecuteAsync(ConfigContext context, Func<Task> next)
	{
		// Сначала выполняем следующие middleware (загрузка и парсинг)
		await next();

		// Если произошла ошибка на предыдущих этапах или конфигурация не загружена, пропускаем валидацию
		if (context.ParsedConfig == null)
		{
			if (!context.HasErrors)
			{
				context.AddError("Configuration not loaded, validation skipped");
			}
			return;
		}

		// Выполняем валидацию
		var validationResult = await _validator.ValidateAsync(context.ParsedConfig, context);

		// Добавляем ошибки из результата валидации в контекст
		foreach (var error in validationResult.Errors)
		{
			context.Errors.Add(error);
		}
	}
}
