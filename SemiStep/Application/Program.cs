using Config;
using Config.Facade;

using Core;

using Domain;
using Domain.Facade;

using Microsoft.Extensions.DependencyInjection;

using S7;

using Serilog;

using Shared;
using Shared.Reasons;

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
		services.AddS7();
		services.AddUi(logger);

		return services.BuildServiceProvider();
	}

	private static async Task<AppConfiguration> InitializeConfigurationAsync(IServiceProvider services)
	{
		var configLoader = services.GetRequiredService<ConfigFacade>();

		const string ConfigDirectory = @"C:\Users\admin\projects\SemiStep\ConfigFiles";

		var context = await configLoader.LoadAsync(ConfigDirectory);

		if (context.HasErrors)
		{
			foreach (var error in context.Errors)
			{
				var location = error is ConfigLoadError configError ? configError.Location : null;
				Log.Error("Error: {Message} at {Location}", error.Message, location ?? "unknown");
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
