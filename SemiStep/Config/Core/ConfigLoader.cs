using SemiStep.Config.Loaders;
using SemiStep.Config.Middleware;
using SemiStep.Config.Models;

using Serilog;

namespace SemiStep.Config.Core;

public sealed class ConfigLoader
{
	private readonly List<IConfigMiddleware> _middlewares;
	private readonly IFileLoader _fileLoader;
	private readonly ILogger? _logger;

	internal ConfigLoader(
		IFileLoader fileLoader,
		IEnumerable<IConfigMiddleware> middlewares,
		ILogger? logger)
	{
		_fileLoader = fileLoader ?? throw new ArgumentNullException(nameof(fileLoader));
		_middlewares = middlewares.ToList();
		_logger = logger;
	}

	public async Task<ConfigContext> LoadAsync(params string[] filePaths)
	{
		if (filePaths == null || filePaths.Length == 0)
		{
			throw new ArgumentException("At least one file path must be provided", nameof(filePaths));
		}

		var context = new ConfigContext
		{
			FilePaths = filePaths.ToList()
		};

		// Построение pipeline через композицию делегатов
		// Финальная операция - загрузка и парсинг файлов
		Func<Task> pipeline = async () => await LoadAndParseAsync(context);

		// Обратная композиция middleware (снаружи внутрь)
		// Последний добавленный middleware будет выполнен первым
		for (int i = _middlewares.Count - 1; i >= 0; i--)
		{
			var middleware = _middlewares[i];
			var next = pipeline;
			pipeline = async () => await middleware.ExecuteAsync(context, next);
		}

		// Запуск pipeline
		await pipeline();

		return context;
	}

	/// <summary>
	/// Внутренний метод для загрузки и парсинга конфигурационных файлов
	/// </summary>
	private async Task LoadAndParseAsync(ConfigContext context)
	{
		// TODO: Реализовать загрузку через IFileLoader
		// 1. Загрузить каждый файл через _fileLoader.LoadAsync()
		// 2. Десериализовать в ConfigRootDto
		// 3. Установить context.ParsedConfig

		_logger?.Debug("Loading configuration files: {FilePaths}", string.Join(", ", context.FilePaths));

		// Заглушка для демонстрации структуры
		await Task.CompletedTask;
	}
}
