using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;

using Tests.Helpers;


namespace Tests.Core.Helpers;

public static class CoreTestHelper
{
	public static async Task<(IServiceProvider Services, RecipeWorkspace Workspace, RecipeEditor Editor, PlcLifecycleManager Plc)> BuildAsync(
		string configName = "Standard")
	{
		var configDir = GetConfigDirectory(configName);

		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var services = new ServiceCollection()
			.AddLogging()
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
		var editor = services.GetRequiredService<RecipeEditor>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		workspace.Reset();

		return (services, workspace, editor, plc);
	}

	private static string GetConfigDirectory(string configName)
	{
		return TestConfigLocator.GetConfigDirectory(configName);
	}
}
