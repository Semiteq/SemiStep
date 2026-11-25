using System.Globalization;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;

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

	/// <summary>The coordinator's 5 s write wait plus 5 s for the PLC disconnect.</summary>
	private static readonly TimeSpan _teardownBound = TimeSpan.FromSeconds(10);

	[STAThread]
	public static int Main(string[] args)
	{
		var options = StartupOptions.Parse(args);
		var exitCode = 0;
		ServiceProvider? provider = null;

		CreateLogger(
			options.LogFilePath,
			options.LoggingLevel
			);

		try
		{
			var outcome = ValidateStartup(options);
			exitCode = outcome.Errors is null ? 0 : 1;
			provider = outcome.Provider;

			// The catch must not re-launch: once App.Run has initialised Avalonia, a second
			// BuildAvaloniaApp() would throw "Application has already been initialized".
			if (outcome.Errors is not null)
			{
				App.RunErrorWindow(outcome.Errors);
			}
			else
			{
				App.Run(provider!);
			}
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Application terminated unexpectedly");
			exitCode = 1;
		}
		finally
		{
			if (provider is not null)
			{
				DisposeServices(provider, _teardownBound);
			}

			Log.CloseAndFlushAsync().AsTask().GetAwaiter().GetResult();
		}

		return exitCode;
	}

	internal static void DisposeServices(ServiceProvider provider, TimeSpan bound)
	{
		try
		{
			var teardown = Task.Run(() => provider.DisposeInShutdownOrderAsync().AsTask());
			if (!teardown.Wait(bound))
			{
				Log.Warning("Service teardown did not finish within {Bound}; exiting anyway", bound);
			}
		}
		catch (AggregateException ex)
		{
			Log.Error(ex.Flatten(), "Service teardown failed");
		}
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
	internal static async Task<Result<StartupConfiguration>> LoadConfigurationAsync(StartupOptions options)
	{
		if (options.PlcEndpointOverride is { IsFailed: true } invalidEndpoint)
		{
			return Result.Fail(invalidEndpoint.Errors);
		}

		var loaded = await ConfigFacade.LoadAndValidateAsync(options.ConfigDir);
		if (loaded.IsFailed)
		{
			return loaded.ToResult<StartupConfiguration>();
		}

		var preset = loaded.Value.PlcConfiguration.Connection;
		if (options.PlcEndpointOverride is not { IsSuccess: true } endpoint)
		{
			return new StartupConfiguration(loaded.Value, new PlcLinkAddress(preset.IpAddress, preset.Port, IsOverride: false));
		}

		var isOverride = endpoint.Value.Host != preset.IpAddress || endpoint.Value.Port != preset.Port;

		return new StartupConfiguration(
			endpoint.Value.ApplyTo(loaded.Value),
			new PlcLinkAddress(endpoint.Value.Host, endpoint.Value.Port, isOverride));
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

		// Set the UI culture from the loaded locale. Only UICulture changes; CurrentCulture
		// stays invariant so number/date formatting and logs remain English.
		var uiCulture = CultureInfo.GetCultureInfo(result.Value.Configuration.Ui.Locale);
		CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
		Resources.Culture = uiCulture;

		return StartupOutcome.Succeeded(BuildServices(options, result.Value));
	}

	internal static ServiceProvider BuildServices(StartupOptions options, StartupConfiguration startup)
	{
		var services =
			new ServiceCollection()
				.AddSingleton(options)
				.AddSingleton(startup.Configuration)
				.AddSingleton(startup.LinkAddress)
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

	private readonly record struct StartupOutcome(ServiceProvider? Provider, IReadOnlyList<string>? Errors)
	{
		public static StartupOutcome Succeeded(ServiceProvider provider)
		{
			return new StartupOutcome(provider, null);
		}

		public static StartupOutcome Failed(IReadOnlyList<string> errors)
		{
			return new StartupOutcome(null, errors);
		}
	}
}
