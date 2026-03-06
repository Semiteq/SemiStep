using Config.Facade;

using Csv.Services;

using Domain.Registries;

using Microsoft.Extensions.DependencyInjection;

using Shared.Registries;

using Tests.Helpers;

namespace Tests.Csv.Helpers;

public static class CsvTestHelper
{
	public static async Task<(CsvSerializer Serializer, IServiceProvider Services)> BuildAsync(
		string configName = "Standard")
	{
		var configDir = TestConfigLocator.GetConfigDirectory(configName);
		var configuration = await ConfigFacade.LoadAndValidateAsync(configDir);

		var services = new ServiceCollection()
			.AddSingleton(configuration)
			.AddSingleton<IActionRegistry>(sp =>
			{
				var registry = new ActionRegistry();
				registry.Initialize(configuration.Actions);
				return registry;
			})
			.AddSingleton<IPropertyRegistry>(sp =>
			{
				var registry = new PropertyRegistry();
				registry.Initialize(configuration.Properties);
				return registry;
			})
			.AddSingleton<IColumnRegistry>(sp =>
			{
				var registry = new ColumnRegistry();
				registry.Initialize(configuration.Columns);
				return registry;
			})
			.AddSingleton<IGroupRegistry>(sp =>
			{
				var registry = new GroupRegistry();
				registry.Initialize(configuration.Groups);
				return registry;
			})
			.AddSingleton<CsvSerializer>()
			.BuildServiceProvider();

		var serializer = services.GetRequiredService<CsvSerializer>();
		return (serializer, services);
	}
}
