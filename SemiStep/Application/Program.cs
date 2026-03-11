using System.Globalization;

using Config;
using Config.Facade;

using Core;

using Csv;

using Domain;
using Domain.Facade;

using Microsoft.Extensions.DependencyInjection;

using S7;

using Serilog;

using UI;

namespace Application;

public static class Program
{
	private const string ConfigDir = @"C:\DISTR\Config\Semistep";
	private const string LogFilePath = @"C:\DISTR\Logs\semistep.log";

	public static async Task Main()
	{
		CreateLogger(LogFilePath);

		try
		{
			var result = await ConfigFacade.LoadAndValidateAsync(ConfigDir);

			if (!result.IsSuccess)
			{
				Log.Error("Application startup failed: configuration loading produced {ErrorCount} error(s)", result.Errors.Count);
				App.RunErrorWindow(result.Errors);
				return;
			}

			var services =
				new ServiceCollection()
					.AddSingleton(result.Configuration!)
					.AddRecipe()
					.AddConfig()
					.AddDomain()
					.AddS7()
					.AddCsv()
					.AddUi();

			var provider = services.BuildServiceProvider();

			InitializeServices(provider);

			App.Run(provider);
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Application terminated unexpectedly");
		}
		finally
		{
			await Log.CloseAndFlushAsync();
		}
	}

	private static void InitializeServices(IServiceProvider provider)
	{
		var domainFacade = provider.GetRequiredService<DomainFacade>();
		domainFacade.Initialize();
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
		catch
		{
			return false;
		}
	}
}
