using Config;
using Config.Facade;

using Core;

using Domain;
using Domain.Facade;
using Domain.Ports;
using Domain.Services;
using Domain.State;

using Microsoft.Extensions.DependencyInjection;

using Serilog;
using Serilog.Core;

using Tests.Helpers;

namespace Tests.Core.Helpers;

public static class CoreTestHelper
{
	public static async Task<(IServiceProvider Services, DomainFacade Facade)> BuildAsync(
		string configName = "Standard")
	{
		var configDir = GetConfigDirectory(configName);

		var silentLogger = new LoggerConfiguration().CreateLogger();

		var services = new ServiceCollection()
			.AddSingleton<ILogger>(silentLogger)
			.AddRecipe()
			.AddConfig()
			.AddDomain()
			.AddSingleton<IPlcConnection, StubPlcConnection>()
			.BuildServiceProvider();

		var configFacade = services.GetRequiredService<ConfigFacade>();
		var context = await configFacade.LoadAsync(configDir);

		if (context.HasErrors || context.Configuration is null)
		{
			var errors = string.Join("; ", context.Errors.Select(e => e.Message));
			throw new InvalidOperationException($"Failed to load config '{configName}': {errors}");
		}

		var domainFacade = services.GetRequiredService<DomainFacade>();
		domainFacade.Initialize(context.Configuration);

		return (services, domainFacade);
	}

	private static string GetConfigDirectory(string configName)
	{
		var baseDir = AppContext.BaseDirectory;

		for (var i = 0; i < 10 && !string.IsNullOrEmpty(baseDir); i++)
		{
			var probe = Path.Combine(baseDir, "YamlConfigs", configName);
			if (Directory.Exists(probe))
			{
				return probe;
			}

			baseDir = Directory.GetParent(baseDir)?.FullName ?? string.Empty;
		}

		throw new DirectoryNotFoundException(
			$"Config directory '{configName}' not found. Expected 'YamlConfigs/{configName}' in or above the test output directory.");
	}
}
