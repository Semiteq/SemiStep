using System.Globalization;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;

using Serilog;

namespace UI;

public static class Program
{
	private const string ConfigDir = @"C:\DISTR\Config\Semistep";
	private const string LogFilePath = @"C:\DISTR\Logs\semistep.log";

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

		return (services.BuildServiceProvider(), null);
	}

	private static void CreateLogger(string logFilePath)
	{
		const string Template = "{Timestamp:O} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
		var invariant = CultureInfo.InvariantCulture;

		if (!EnsureLogDirExists(logFilePath))
		{
			return;
		}

		var config =
			new LoggerConfiguration()
				.MinimumLevel.Verbose()
				.Enrich.FromLogContext()
				.WriteTo.Console();

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
