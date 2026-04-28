using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;

using Tests.Helpers;


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
			.AddCsv()
			.AddClipboard()
			.AddSingleton<StubS7Service>()
			.AddSingleton<IS7Connection>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7Reader>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7ExecutionStream>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IPlcSyncService, StubPlcSyncService>()
			.BuildServiceProvider();

		var workspace = services.GetRequiredService<RecipeWorkspace>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		workspace.Reset();

		var fileSerializer = services.GetRequiredService<CsvFileSerializer>();
		var clipboardSerializer = services.GetRequiredService<ClipboardSerializer>();
		return (fileSerializer, clipboardSerializer, services);
	}
}
