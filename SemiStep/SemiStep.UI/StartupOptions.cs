using FluentResults;

using Serilog.Events;

namespace SemiStep.UI;

public sealed record StartupOptions(
	string ConfigDir,
	string LogFilePath,
	LogEventLevel LoggingLevel,
	Result<PlcEndpoint>? PlcEndpointOverride = null)
{
	private const string PlcEndpointOption = "--plc-endpoint";
	private const string PlcEndpointAssignmentPrefix = PlcEndpointOption + "=";

	public const string DefaultConfigDir =
		@"C:\DISTR\Config\SemiStep\MBE";

	public const string DefaultLogFilePath =
		@"C:\DISTR\Logs\SemiStep\semistep.log";

	public const LogEventLevel DefaultLoggingLevel =
		LogEventLevel.Warning;

	public static StartupOptions Parse(string[] args)
	{
		var configDir = DefaultConfigDir;
		var logFilePath = DefaultLogFilePath;
		var logLevel = DefaultLoggingLevel;
		Result<PlcEndpoint>? plcEndpoint = null;

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

				case PlcEndpointOption:
					plcEndpoint = ParsePlcEndpoint(i + 1 < args.Length ? args[++i] : null);
					break;

				case var argument when argument.StartsWith(PlcEndpointAssignmentPrefix, StringComparison.Ordinal):
					plcEndpoint = ParsePlcEndpoint(argument[PlcEndpointAssignmentPrefix.Length..]);
					break;
			}
		}

		return new StartupOptions(
			configDir,
			logFilePath,
			logLevel,
			plcEndpoint);
	}

	private static Result<PlcEndpoint> ParsePlcEndpoint(string? value)
	{
		var parsed = PlcEndpoint.Parse(value);
		return parsed.IsSuccess
			? parsed
			: Result.Fail(
				$"Option {PlcEndpointOption} requires a value in the form host:port: {parsed.Errors[0].Message}.");
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
