using System.Diagnostics;

using Config.Models;

using Serilog;
using Serilog.Events;

namespace Config.Middleware;

/// <summary>
/// Middleware для логирования процесса загрузки конфигурации
/// </summary>
public sealed class LoggingMiddleware : IConfigMiddleware
{
	private readonly ILogger? _logger;
	private readonly LogEventLevel _minLevel;

	public string Name => "Logging";

	public LoggingMiddleware(ILogger? logger, LogEventLevel minLevel = LogEventLevel.Information)
	{
		_logger = logger;
		_minLevel = minLevel;
	}

	public async Task ExecuteAsync(ConfigContext context, Func<Task> next)
	{
		if (_logger == null)
		{
			await next();
			return;
		}

		var stopwatch = Stopwatch.StartNew();

		_logger.Write(_minLevel, "Starting configuration loading from {FilePaths}",
			string.Join(", ", context.FilePaths));

		await next();

		stopwatch.Stop();

		if (context.HasErrors)
		{
			var errorCount = context.Errors.Count(e => e.Severity == ErrorSeverity.Error);
			var warningCount = context.Errors.Count(e => e.Severity == ErrorSeverity.Warning);

			_logger.Error(
				"Configuration loading completed with {ErrorCount} error(s), {WarningCount} warning(s) in {ElapsedMs}ms",
				errorCount, warningCount, stopwatch.ElapsedMilliseconds);

			// Логируем каждую ошибку на уровне Error
			foreach (var error in context.Errors.Where(e => e.Severity == ErrorSeverity.Error))
			{
				_logger.Error("  {ErrorMessage} {Location}",
					error.Message,
					error.Location != null ? $"at {error.Location}" : string.Empty);
			}

			// Логируем предупреждения, если уровень логирования достаточен
			if (_minLevel <= LogEventLevel.Warning)
			{
				foreach (var warning in context.Errors.Where(e => e.Severity == ErrorSeverity.Warning))
				{
					_logger.Warning("  {WarningMessage} {Location}",
						warning.Message,
						warning.Location != null ? $"at {warning.Location}" : string.Empty);
				}
			}
		}
		else
		{
			_logger.Write(_minLevel,
				"Configuration loaded successfully in {ElapsedMs}ms",
				stopwatch.ElapsedMilliseconds);
		}
	}
}
