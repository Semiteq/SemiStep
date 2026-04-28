using ClipBoard;

using Config;
using Config.Facade;

using Core;

using Csv;

using Domain;
using Domain.Facade;
using Domain.Plc;

using Microsoft.Extensions.DependencyInjection;

using Tests.Helpers;

using TypesShared.Domain;

namespace Tests.Core.Helpers;

public static class CoreTestHelper
{
	public static async Task<(IServiceProvider Services, RecipeWorkspace Workspace, RecipeEditor Editor, PlcLifecycleManager Plc)> BuildAsync(
		string configName = "Standard")
	{
		var configDir = GetConfigDirectory(configName);

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
