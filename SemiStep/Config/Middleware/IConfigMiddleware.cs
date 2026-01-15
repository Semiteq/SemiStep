using Config.Models;

namespace Config.Middleware;

/// <summary>
/// Базовый интерфейс для middleware в конвейере загрузки конфигурации
/// </summary>
public interface IConfigMiddleware
{
	/// <summary>
	/// Название middleware для логирования и отладки
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Выполняет обработку контекста конфигурации
	/// </summary>
	/// <param name="context">Контекст с данными конфигурации</param>
	/// <param name="next">Следующий делегат в pipeline</param>
	Task ExecuteAsync(ConfigContext context, Func<Task> next);
}
