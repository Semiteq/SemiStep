using Config;
using Config.Facade;

using Core;

using Csv;

using Domain;
using Domain.Facade;

using Microsoft.Extensions.DependencyInjection;

using S7;

using Serilog;

using Shared;
using Shared.Reasons;

using UI;

namespace Application;

public static class Program
{
	public static void Main()
	{
		var logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console()
			.CreateLogger();

		Log.Logger = logger;

		try
		{
			var services = new ServiceCollection();

			services
				.AddRecipe()
				.AddConfig()
				.AddDomain()
				.AddS7()
				.AddCsv()
				.AddUi()
				.AddSingleton(logger);

			var configuration = LoadConfigurationAsync(services).GetAwaiter().GetResult();

			services.AddSingleton(configuration.PlcConfiguration);

			var provider = services.BuildServiceProvider();

			InitializeServices(provider, configuration);

			RunAvaloniaApp(provider, configuration);
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

	private static async Task<AppConfiguration> LoadConfigurationAsync(ServiceCollection services)
	{
		var tempProvider = services.BuildServiceProvider();
		var configLoader = tempProvider.GetRequiredService<ConfigFacade>();

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

		Log.Information("Configuration loaded successfully");

		return context.Configuration;
	}

	private static void InitializeServices(IServiceProvider provider, AppConfiguration configuration)
	{
		var domainFacade = provider.GetRequiredService<DomainFacade>();
		domainFacade.Initialize(configuration);
	}

	private static void RunAvaloniaApp(IServiceProvider services, AppConfiguration configuration)
	{
		App.Run(services, configuration);
	}
}
