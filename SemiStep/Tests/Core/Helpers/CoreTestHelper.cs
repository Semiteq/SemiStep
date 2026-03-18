using Config;
using Config.Facade;

using Core;

using Domain;
using Domain.Facade;

using Microsoft.Extensions.DependencyInjection;

using Shared.ServiceContracts;

using Tests.Helpers;

namespace Tests.Core.Helpers;

public static class CoreTestHelper
{
	public static async Task<(IServiceProvider Services, DomainFacade Facade)> BuildAsync(
		string configName = "Standard")
	{
		var configDir = GetConfigDirectory(configName);

		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var services = new ServiceCollection()
			.AddSingleton(configLoadResult.Value)
			.AddRecipe()
			.AddDomain()
			.AddSingleton<ICsvService, StubCsvService>()
			.AddSingleton<ICsvClipboardService, StubCsvClipboardService>()
			.AddSingleton<IS7ConnectionService, StubS7ConnectionService>()
			.BuildServiceProvider();

		var domainFacade = services.GetRequiredService<DomainFacade>();
		domainFacade.Initialize();

		return (services, domainFacade);
	}

	private static string GetConfigDirectory(string configName)
	{
		return TestConfigLocator.GetConfigDirectory(configName);
	}
}
