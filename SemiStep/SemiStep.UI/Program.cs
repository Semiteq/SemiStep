using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
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

	[STAThread]
	public static void Main(string[] args)
	{
		// Baseline UI culture before config load. Harmless: the only pre-config UI is
		// ErrorWindow, which is hardcoded English and does not depend on culture/resx.
		CultureInfo.DefaultThreadCurrentUICulture = UiCultureSelector.Resolve(null);

		var options = StartupOptions.Parse(args);

		CreateLogger(
			options.LogFilePath,
			options.LoggingLevel
			);

		try
		{
			var outcome = ValidateStartup(options);

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
		}
		finally
		{
			Log.CloseAndFlushAsync().GetAwaiter().GetResult();
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

	private static async Task<StartupOutcome> StartupAsync(StartupOptions options)
	{
		var result = await ConfigFacade.LoadAndValidateAsync(options.ConfigDir);

		if (result.IsFailed)
		{
			var errors = result.Errors.Select(e => e.Message).ToList();
			Log.Error(
				"Application startup failed: configuration loading produced {ErrorCount} error(s)",
				errors.Count);

			return StartupOutcome.Failed(errors);
		}

		// Override the UI culture from the loaded locale. Only UICulture changes; CurrentCulture
		// stays invariant so number/date formatting and logs remain English.
		var uiCulture = UiCultureSelector.Resolve(result.Value.Ui.Locale);
		CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
		Resources.Culture = uiCulture;

		var services =
			new ServiceCollection()
				.AddSingleton(options)
				.AddSingleton(result.Value)
				.AddRecipe()
				.AddS7()
				.AddCsv()
				.AddClipboard()
				.AddUi();

		services.AddLogging(b => b.AddSerilog(Log.Logger, dispose: false));

		return StartupOutcome.Succeeded(services.BuildServiceProvider());
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
			new LoggerConfiguration()
				.MinimumLevel.Is(logLevel)
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
