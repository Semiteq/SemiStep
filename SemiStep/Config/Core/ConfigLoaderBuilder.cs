using Config.Loaders;
using Config.Middleware;

using Serilog;
using Serilog.Events;

namespace Config.Core;

public sealed class ConfigLoaderBuilder
{
	private IFileLoader? _fileLoader;
	private readonly List<IConfigMiddleware> _middlewares = new();
	private ILogger? _logger;

	public ConfigLoaderBuilder UseYaml()
	{
		_fileLoader = new YamlFileLoader();
		return this;
	}

	/// <summary>
	/// Устанавливает logger для логирования процесса загрузки
	/// </summary>
	public ConfigLoaderBuilder WithLogger(ILogger logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		return this;
	}

	/// <summary>
	/// Добавляет middleware для обработки исключений
	/// </summary>
	/// <param name="stopOnError">Останавливать ли pipeline при первой ошибке</param>
	public ConfigLoaderBuilder AddExceptionHandling(bool stopOnError = false)
	{
		_middlewares.Add(new ExceptionHandlingMiddleware(_logger, stopOnError));
		return this;
	}

	/// <summary>
	/// Добавляет middleware для логирования процесса загрузки
	/// </summary>
	/// <param name="minLevel">Минимальный уровень логирования</param>
	public ConfigLoaderBuilder AddLogging(LogEventLevel minLevel = LogEventLevel.Information)
	{
		_middlewares.Add(new LoggingMiddleware(_logger, minLevel));
		return this;
	}

	/// <summary>
	/// Добавляет middleware для мерджа конфигураций из нескольких файлов
	/// </summary>
	public ConfigLoaderBuilder AddMerge()
	{
		_middlewares.Add(new MergeMiddleware());
		return this;
	}

	/// <summary>
	/// Добавляет middleware для валидации конфигурации
	/// </summary>
	public ConfigLoaderBuilder AddValidation()
	{
		var validator = new Validation.ConfigSchemaValidator(
			new Validation.RangeValidator(),
			new Validation.LinkageValidator()
		);
		_middlewares.Add(new ValidationMiddleware(validator));
		return this;
	}

	/// <summary>
	/// Создает экземпляр ConfigLoader с настроенным pipeline
	/// </summary>
	public ConfigLoader Build()
	{
		if (_fileLoader is null)
			throw new InvalidOperationException("File loader not configured. Call UseYaml() or other loader configuration method.");

		return new ConfigLoader(_fileLoader, _middlewares, _logger);
	}
}
