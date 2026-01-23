using Config;
using Config.Facade;

using Domain;

using Microsoft.Extensions.DependencyInjection;

using Recipe;

using Serilog;

namespace SemiStep.Application;

public class Program
{
	public static async Task Main(string[] args)
	{
		var logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console()
			.CreateLogger();

		Log.Logger = logger;

		try
		{
			var services = ConfigureServices(logger);
			await RunAsync(services);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Application terminated unexpectedly");
		}
		finally
		{
			await Log.CloseAndFlushAsync();
		}
	}

	private static IServiceProvider ConfigureServices(ILogger logger)
	{
		var services = new ServiceCollection();

		services.AddSingleton(logger);

		services.AddCore(logger);
		services.AddConfig(logger);
		services.AddDomain(logger);

		return services.BuildServiceProvider();
	}

	private static async Task RunAsync(IServiceProvider services)
	{
		var configLoader = services.GetRequiredService<ConfigFacade>();
		var domain = services.GetRequiredService<DomainFacade>();

		const string ConfigDirectory = @"C:\Users\admin\Projects\git\SemiStep\ConfigFiles";

		var context = await configLoader.LoadAsync(ConfigDirectory);

		if (context.HasErrors)
		{
			foreach (var error in context.Errors)
			{
				Log.Error("{Severity}: {Message} at {Location}", error.Severity, error.Message, error.Location);
			}

			return;
		}

		if (context.Configuration is null)
		{
			Log.Error("Configuration is null after successful loading");
			return;
		}

		domain.Initialize(context.Configuration);

		Log.Information("Application initialized successfully");
	}
}
