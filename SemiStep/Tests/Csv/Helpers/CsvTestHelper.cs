using Config;
using Config.Facade;

using Core;

using Csv.ClipboardService;
using Csv.FsService;

using Domain;
using Domain.Facade;

using Microsoft.Extensions.DependencyInjection;

using Shared.ServiceContracts;

using Tests.Helpers;

namespace Tests.Csv.Helpers;

internal static class CsvTestHelper
{
	public static async Task<(CsvFileSerializer Serializer, CsvClipboardSerializer ClipboardSerializer, IServiceProvider Services)> BuildAsync(
		string configName = "Standard")
	{
		var configDir = TestConfigLocator.GetConfigDirectory(configName);
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var services = new ServiceCollection()
			.AddSingleton(configLoadResult.Value)
			.AddRecipe()
			.AddDomain()
			.AddSingleton<ICsvService, StubCsvService>()
			.AddSingleton<IS7ConnectionService, StubS7ConnectionService>()
			.AddSingleton<CsvFileSerializer>()
			.AddSingleton<CsvClipboardSerializer>()
			.BuildServiceProvider();

		var domainFacade = services.GetRequiredService<DomainFacade>();
		domainFacade.Initialize();

		var serializer = services.GetRequiredService<CsvFileSerializer>();
		var clipboardSerializer = services.GetRequiredService<CsvClipboardSerializer>();
		return (serializer, clipboardSerializer, services);
	}
}
