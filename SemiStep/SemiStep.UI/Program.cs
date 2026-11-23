using System.Globalization;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;

using SemiStep.UI.Localization;

using Serilog;
using Serilog.Events;

namespace SemiStep.UI;

public static class Program
{
	/// <summary>
	/// The PLC reconciliation source, pinned so its begin and outcome lines survive the field's
	/// default <c>Warning</c> floor.
	/// </summary>
	internal static readonly string PinnedReconciliationSource = typeof(PlcLifecycleManager).FullName!;

	[STAThread]
	public static int Main(string[] args)
	{
		// Baseline UI culture before config load. Harmless: the only pre-config UI is
		// ErrorWindow, which is hardcoded English and does not depend on culture/resx.
		CultureInfo.DefaultThreadCurrentUICulture = UiCultureSelector.Resolve(null);

		var options = StartupOptions.Parse(args);
		var exitCode = 0;

		CreateLogger(
			options.LogFilePath,
			options.LoggingLevel
			);

		try
		{
			var outcome = ValidateStartup(options);
			exitCode = outcome.Errors is null ? 0 : 1;

			// The catch must not re-launch: once App.Run has initialised Avalonia, a second
			// BuildAvaloniaApp() would throw "Application has already been initialized".
			if (outcome.Errors is not null)
			{
				App.RunErrorWindow(outcome.Errors);
			}
			else
			{
				App.Run(outcome.Provider!);
			}
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Application terminated unexpectedly");
			exitCode = 1;
		}
		finally
		{
			Log.CloseAndFlushAsync().AsTask().GetAwaiter().GetResult();
		}

		return exitCode;
	}

	private static StartupOutcome ValidateStartup(StartupOptions options)
	{
		try
		{
			return Task.Run(() => StartupAsync(options)).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Startup validation failed before UI initialisation");
			return StartupOutcome.Failed(["Application startup failed unexpectedly:", ex.Message]);
		}
	}

	/// <summary>A malformed PLC endpoint option fails before the configuration loads, so it is never silently dropped.</summary>
	internal static async Task<Result<AppConfiguration>> LoadConfigurationAsync(StartupOptions options)
	{
		if (options.PlcEndpointOverride is { IsFailed: true } invalidEndpoint)
		{
			return Result.Fail(invalidEndpoint.Errors);
		}

		var loaded = await ConfigFacade.LoadAndValidateAsync(options.ConfigDir);
		return loaded.IsSuccess && options.PlcEndpointOverride is { IsSuccess: true } endpoint
			? Result.Ok(endpoint.Value.ApplyTo(loaded.Value))
			: loaded;
	}

	private static async Task<StartupOutcome> StartupAsync(StartupOptions options)
	{
		var result = await LoadConfigurationAsync(options);

		if (result.IsFailed)
		{
			var errors = result.Errors.Select(e => e.Message).ToList();
			Log.Error(
				"Application startup failed: configuration loading produced {ErrorCount} error(s)",
				errors.Count);

			foreach (var error in errors)
			{
				Log.Error("Configuration error: {Error}", error);
			}

			foreach (var exceptional in result.Errors.SelectMany(e => e.Reasons).OfType<ExceptionalError>())
			{
				Log.Error(exceptional.Exception, "Configuration load exception");
			}

			return StartupOutcome.Failed(errors);
		}

		Log.Information("Configuration loaded successfully");

		// Override the UI culture from the loaded locale. Only UICulture changes; CurrentCulture
		// stays invariant so number/date formatting and logs remain English.
		var uiCulture = UiCultureSelector.Resolve(result.Value.Ui.Locale);
		CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
		Resources.Culture = uiCulture;

		return StartupOutcome.Succeeded(BuildServices(options, result.Value));
	}

	internal static ServiceProvider BuildServices(StartupOptions options, AppConfiguration configuration)
	{
		var services =
			new ServiceCollection()
				.AddSingleton(options)
				.AddSingleton(configuration)
				.AddRecipe()
				.AddS7()
				.AddCsv()
				.AddClipboard()
				.AddUi();

		services.AddLogging(b => b.AddSerilog(Log.Logger, dispose: false));

		return services.BuildServiceProvider();
	}

	private static void CreateLogger(
		string logFilePath,
		LogEventLevel logLevel)
	{
		const string Template =
			"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

		var invariant = CultureInfo.InvariantCulture;

		if (!EnsureLogDirExists(logFilePath))
		{
			return;
		}

		var config =
			ApplyMinimumLevels(new LoggerConfiguration(), logLevel)
				.Enrich.FromLogContext()
				.WriteTo.File(
					path: logFilePath,
					rollingInterval: RollingInterval.Infinite,
					fileSizeLimitBytes: 5 * 1024 * 1024,
					rollOnFileSizeLimit: true,
					retainedFileCountLimit: 5,
					shared: true,
					outputTemplate: Template,
					formatProvider: invariant);

		Log.Logger = config.CreateLogger();
	}

	internal static LoggerConfiguration ApplyMinimumLevels(LoggerConfiguration config, LogEventLevel logLevel)
	{
		return config
			.MinimumLevel.Is(logLevel)
			.MinimumLevel.Override(PinnedReconciliationSource, ResolvePinnedSourceLevel(logLevel));
	}

	/// <summary>
	/// A Serilog per-source override replaces the effective minimum in both directions: it raises the
	/// floor as readily as it lowers it. Pinning at <c>Information</c> unconditionally would therefore
	/// drop this source's <c>Debug</c> lines even under <c>--logging-level debug</c>, so the pin is the
	/// lower of the global level and <c>Information</c>.
	/// </summary>
	internal static LogEventLevel ResolvePinnedSourceLevel(LogEventLevel logLevel)
	{
		return logLevel < LogEventLevel.Information ? logLevel : LogEventLevel.Information;
	}

	private static bool EnsureLogDirExists(string filePath)
	{
		try
		{
			var directory = Path.GetDirectoryName(filePath);
			if (directory is not null)
			{
				Directory.CreateDirectory(directory);
			}

			return true;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Failed to create log directory for '{filePath}': {ex.Message}. File logging is disabled.");
			return false;
		}
	}

	private readonly record struct StartupOutcome(IServiceProvider? Provider, IReadOnlyList<string>? Errors)
	{
		public static StartupOutcome Succeeded(IServiceProvider provider)
		{
			return new StartupOutcome(provider, null);
		}

		public static StartupOutcome Failed(IReadOnlyList<string> errors)
		{
			return new StartupOutcome(null, errors);
		}
	}
}
