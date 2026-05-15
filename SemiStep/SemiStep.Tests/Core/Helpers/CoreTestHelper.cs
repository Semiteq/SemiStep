using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Helpers;

namespace SemiStep.Tests.Core.Helpers;

public static class CoreTestHelper
{
	public static async Task<(IServiceProvider Services, RecipeSession Session, PlcLifecycleManager Plc)> BuildAsync(
		string configName = "Standard")
	{
		var services = await BuildServicesAsync(configName);

		var session = services.GetRequiredService<RecipeSession>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		session.Reset().EnsureSuccess("Session reset");

		return (services, session, plc);
	}

	private static async Task<IServiceProvider> BuildServicesAsync(string configName)
	{
		var configDir = GetConfigDirectory(configName);

		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);
		var configuration = configLoadResult.EnsureSuccess("Test config load");

		return new ServiceCollection()
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
	}

	private static string GetConfigDirectory(string configName)
	{
		return TestConfigLocator.GetConfigDirectory(configName);
	}
}
