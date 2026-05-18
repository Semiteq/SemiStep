using Serilog.Events;

namespace SemiStep.UI;

public sealed record StartupOptions(
	string ConfigDir,
	string LogFilePath,
	LogEventLevel LoggingLevel)
{
	public const string DefaultConfigDir =
		@"C:\DISTR\Config\Semistep\MBE";

	public const string DefaultLogFilePath =
		@"C:\DISTR\Logs\Semistep\semistep.log";

	public const LogEventLevel DefaultLoggingLevel =
		LogEventLevel.Warning;

	public static StartupOptions Parse(string[] args)
	{
		var configDir = DefaultConfigDir;
		var logFilePath = DefaultLogFilePath;
		var logLevel = DefaultLoggingLevel;

		for (var i = 0; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--config-dir" when i + 1 < args.Length:
					configDir = args[++i];
					break;

				case "--log-file" when i + 1 < args.Length:
					logFilePath = args[++i];
					break;

				case "--logging-level" when i + 1 < args.Length:
					logLevel = ParseLogLevel(args[++i]);
					break;
			}
		}

		return new StartupOptions(
			configDir,
			logFilePath,
			logLevel);
	}

	private static LogEventLevel ParseLogLevel(string value)
	{
		return value.ToLowerInvariant() switch
		{
			"verbose" => LogEventLevel.Verbose,
			"debug" => LogEventLevel.Debug,
			"info" => LogEventLevel.Information,
			"warning" => LogEventLevel.Warning,
			"error" => LogEventLevel.Error,
			"fatal" => LogEventLevel.Fatal,
			_ => DefaultLoggingLevel
		};
	}
}
