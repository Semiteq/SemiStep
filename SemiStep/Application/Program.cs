using Config;
using Config.Facade;

using Domain;
using Domain.Facade;

using Microsoft.Extensions.DependencyInjection;

using Recipe;

using Serilog;

using Shared;

using UI;

namespace SemiStep.Application;

public class Program
{
	public static void Main(string[] args)
	{
		var logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console()
			.CreateLogger();

		Log.Logger = logger;

		try
		{
			var services = ConfigureServices(logger);
			var configuration = InitializeConfigurationAsync(services).GetAwaiter().GetResult();
			RunAvaloniaApp(services, configuration);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Application terminated unexpectedly");
		}
		finally
		{
			Log.CloseAndFlush();
		}
	}

	private static IServiceProvider ConfigureServices(ILogger logger)
	{
		var services = new ServiceCollection();

		services.AddSingleton(logger);

		services.AddRecipe(logger);
		services.AddConfig(logger);
		services.AddDomain(logger);
		services.AddUi();

		return services.BuildServiceProvider();
	}

	private static async Task<AppConfiguration> InitializeConfigurationAsync(IServiceProvider services)
	{
		var configLoader = services.GetRequiredService<ConfigFacade>();

		const string ConfigDirectory = @"C:\Users\admin\Projects\git\SemiStep\ConfigFiles";

		var context = await configLoader.LoadAsync(ConfigDirectory);

		if (context.HasErrors)
		{
			foreach (var error in context.Errors)
			{
				Log.Error("{Severity}: {Message} at {Location}", error.Severity, error.Message, error.Location);
			}

			throw new InvalidOperationException("Configuration loading failed with errors");
		}

		if (context.Configuration is null)
		{
			Log.Error("Configuration is null after successful loading");
			throw new InvalidOperationException("Configuration is null");
		}

		var domainFacade = services.GetRequiredService<DomainFacade>();
		domainFacade.Initialize(context.Configuration);

		Log.Information("Configuration loaded successfully");

		return context.Configuration;
	}

	private static void RunAvaloniaApp(IServiceProvider services, AppConfiguration configuration)
	{
		App.Run(services, configuration);
	}
}
