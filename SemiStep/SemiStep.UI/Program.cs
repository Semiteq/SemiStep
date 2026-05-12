using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;

using Serilog;

namespace SemiStep.UI;

public static class Program
{
	private const string ConfigDir = @"C:\DISTR\Config\Semistep";
	private const string LogFilePath = @"C:\DISTR\Logs\Semistep\semistep.log";

	[STAThread]
	public static void Main()
	{
		CreateLogger(LogFilePath);

		try
		{
			var outcome = Task.Run(StartupAsync).GetAwaiter().GetResult();

			if (outcome.Errors is not null)
			{
				App.RunErrorWindow(outcome.Errors);
			}
			else if (outcome.Provider is not null)
			{
				App.Run(outcome.Provider);
			}
			else
			{
				App.RunErrorWindow(["Application startup failed: unknown error"]);
			}
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Application terminated unexpectedly");

			// If Avalonia was already initialized before the exception, a second
			// StartWithClassicDesktopLifetime throws "Application has already been initialized".
			// Swallow that secondary failure so the original exception is the one logged.
			try
			{
				App.RunErrorWindow(["Application startup failed unexpectedly:", ex.Message]);
			}
			catch (Exception secondary)
			{
				Log.Fatal(secondary, "Failed to display error window after primary failure");
			}
		}
		finally
		{
			Log.CloseAndFlushAsync().GetAwaiter().GetResult();
		}
	}

	private static async Task<(IServiceProvider? Provider, IReadOnlyList<string>? Errors)> StartupAsync()
	{
		var result = await ConfigFacade.LoadAndValidateAsync(ConfigDir);

		if (result.IsFailed)
		{
			var errors = result.Errors.Select(e => e.Message).ToList();
			Log.Error(
				"Application startup failed: configuration loading produced {ErrorCount} error(s)",
				errors.Count);

			return (null, errors);
		}

		var services =
			new ServiceCollection()
				.AddSingleton(result.Value)
				.AddRecipe()
				.AddS7()
				.AddCsv()
				.AddClipboard()
				.AddUi();

		services.AddLogging(b => b.AddSerilog(Log.Logger, dispose: false));

		return (services.BuildServiceProvider(), null);
	}

	private static void CreateLogger(string logFilePath)
	{
		const string Template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
		var invariant = CultureInfo.InvariantCulture;

		if (!EnsureLogDirExists(logFilePath))
		{
			return;
		}

		var config =
			new LoggerConfiguration()
				.MinimumLevel.Verbose()
				.Enrich.FromLogContext()
				.WriteTo.Console(outputTemplate: Template, formatProvider: invariant);

		config = config.WriteTo.File(
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
}
