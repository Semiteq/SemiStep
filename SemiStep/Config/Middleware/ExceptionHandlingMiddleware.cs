using SemiStep.Config.Models;

using Serilog;

using YamlDotNet.Core;

namespace SemiStep.Config.Middleware;

/// <summary>
/// Middleware для перехвата и обработки исключений в pipeline
/// Самый внешний слой - оборачивает весь процесс загрузки
/// </summary>
public sealed class ExceptionHandlingMiddleware : IConfigMiddleware
{
	private readonly ILogger? _logger;
	private readonly bool _stopOnError;

	public string Name => "ExceptionHandling";

	public ExceptionHandlingMiddleware(ILogger? logger, bool stopOnError = false)
	{
		_logger = logger;
		_stopOnError = stopOnError;
	}

	public async Task ExecuteAsync(ConfigContext context, Func<Task> next)
	{
		try
		{
			await next();
		}
		catch (YamlException ex)
		{
			var location = ex.Start != null ? $"Line {ex.Start.Line}, Column {ex.Start.Column}" : null;
			context.AddError($"YAML parsing error: {ex.Message}", location);
			_logger?.Error(ex, "YAML parsing failed at {Location}", location);

			if (_stopOnError)
				return;
		}
		catch (FileNotFoundException ex)
		{
			context.AddError($"Configuration file not found: {ex.FileName}");
			_logger?.Error(ex, "Configuration file not found: {FileName}", ex.FileName);

			if (_stopOnError)
				return;
		}
		catch (IOException ex)
		{
			context.AddError($"IO error while reading configuration: {ex.Message}");
			_logger?.Error(ex, "IO error during configuration loading");

			if (_stopOnError)
				return;
		}
		catch (Exception ex)
		{
			context.AddError($"Unexpected error: {ex.Message}");
			_logger?.Error(ex, "Unexpected error during configuration loading");

			if (_stopOnError)
				return;
		}
	}
}
