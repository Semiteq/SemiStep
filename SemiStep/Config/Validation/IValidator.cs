using Config.Models;

namespace Config.Validation;

/// <summary>
/// Базовый интерфейс для валидаторов конфигурации
/// </summary>
public interface IValidator
{
	/// <summary>
	/// Выполняет валидацию объекта конфигурации
	/// </summary>
	/// <param name="config">Объект конфигурации для валидации</param>
	/// <param name="context">Контекст загрузки конфигурации</param>
	/// <returns>Результат валидации с набором ошибок и предупреждений</returns>
	Task<ValidationResult> ValidateAsync(object config, ConfigContext context);
}
