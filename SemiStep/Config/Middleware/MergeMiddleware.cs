using SemiStep.Config.Dto;
using SemiStep.Config.Models;

namespace SemiStep.Config.Middleware;

/// <summary>
/// Middleware для слияния конфигураций из нескольких файлов
/// </summary>
public sealed class MergeMiddleware : IConfigMiddleware
{
	public string Name => "Merge";

	public async Task ExecuteAsync(ConfigContext context, Func<Task> next)
	{
		// Сначала выполняем следующие middleware (загрузка файлов)
		await next();

		// Если загружен только один файл или произошла ошибка, пропускаем мерж
		if (context.FilePaths.Count <= 1 || context.ParsedConfig == null)
			return;

		// TODO: Реализовать логику мерджа конфигураций
		// 1. Если в context.Metadata есть несколько загруженных ConfigRootDto
		// 2. Объединить их по следующим правилам:
		//    - Actions с одинаковым Key: последний выигрывает (override)
		//    - Properties внутри Actions: также последний выигрывает
		// 3. Установить результат в context.ParsedConfig

		// Пример структуры для хранения промежуточных данных:
		// context.Metadata["LoadedConfigs"] = List<ConfigRootDto>

		await Task.CompletedTask;
	}

	/// <summary>
	/// Объединяет две конфигурации (приоритет у второй)
	/// </summary>
	private ConfigRootDto MergeConfigs(ConfigRootDto baseConfig, ConfigRootDto overrideConfig)
	{
		// TODO: Реализовать логику глубокого мерджа
		throw new NotImplementedException("Merge logic not implemented");
	}
}
