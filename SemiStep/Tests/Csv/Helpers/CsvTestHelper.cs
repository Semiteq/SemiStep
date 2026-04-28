using ClipBoard;

using Config;
using Config.Facade;

using Core;

using Csv;

using Domain;
using Domain.Facade;

using Microsoft.Extensions.DependencyInjection;

using Tests.Helpers;

using TypesShared.Domain;

namespace Tests.Csv.Helpers;

internal static class CsvTestHelper
{
	public static async Task<(CsvFileSerializer FileSerializer, ClipboardSerializer ClipboardSerializer, IServiceProvider Services)> BuildAsync(
		string configName = "Standard")
	{
		var configDir = TestConfigLocator.GetConfigDirectory(configName);
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var services = new ServiceCollection()
			.AddSingleton(configLoadResult.Value)
			.AddRecipe()
			.AddDomain()
			.AddCsv()
			.AddClipboard()
			.AddSingleton<IS7Service, StubIs7Service>()
			.AddSingleton<IPlcSyncService, StubPlcSyncService>()
			.BuildServiceProvider();

		var domainFacade = services.GetRequiredService<DomainFacade>();
		domainFacade.Initialize();

		var fileSerializer = services.GetRequiredService<CsvFileSerializer>();
		var clipboardSerializer = services.GetRequiredService<ClipboardSerializer>();
		return (fileSerializer, clipboardSerializer, services);
	}
}
