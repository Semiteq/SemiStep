using Config;
using Config.Facade;

using Core;

using Domain;
using Domain.Facade;
using Domain.Ports;

using Microsoft.Extensions.DependencyInjection;

using Tests.Helpers;

namespace Tests.Core.Helpers;

public static class CoreTestHelper
{
	public static async Task<(IServiceProvider Services, DomainFacade Facade)> BuildAsync(
		string configName = "Standard")
	{
		var configDir = GetConfigDirectory(configName);

		var configuration = await ConfigFacade.LoadAndValidateAsync(configDir);

		var services = new ServiceCollection()
			.AddSingleton(configuration)
			.AddRecipe()
			.AddConfig()
			.AddDomain()
			.AddSingleton<ICsvService, StubCsvService>()
			.AddSingleton<IS7ConnectionService, StubS7ConnectionService>()
			.BuildServiceProvider();

		var domainFacade = services.GetRequiredService<DomainFacade>();
		domainFacade.Initialize();

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
