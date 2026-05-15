using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Helpers;

namespace SemiStep.Tests.Csv.Helpers;

internal static class CsvTestHelper
{
	public static async Task<(CsvFileSerializer FileSerializer, ClipboardSerializer ClipboardSerializer, IServiceProvider Services)> BuildAsync(
		string configName = "Standard")
	{
		var configDir = TestConfigLocator.GetConfigDirectory(configName);
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);
		var configuration = configLoadResult.EnsureSuccess("Test config load");

		var services = new ServiceCollection()
			.AddLogging()
			.AddSingleton(configuration)
			.AddRecipe()
			.AddCsv()
			.AddClipboard()
			.AddSingleton<StubS7Service>()
			.AddSingleton<IS7Connection>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7Reader>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7ExecutionStream>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IPlcSyncService, StubPlcSyncService>()
			.BuildServiceProvider();

		var session = services.GetRequiredService<RecipeSession>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		session.Reset().EnsureSuccess("Session reset");

		var fileSerializer = services.GetRequiredService<CsvFileSerializer>();
		var clipboardSerializer = services.GetRequiredService<ClipboardSerializer>();
		return (fileSerializer, clipboardSerializer, services);
	}
}
