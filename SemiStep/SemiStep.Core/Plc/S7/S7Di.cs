using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.S7;

public static class S7Di
{
	public static IServiceCollection AddS7(this IServiceCollection services)
	{
		services.AddSingleton<S7Driver>();
		services.AddSingleton<IS7Transport>(sp => sp.GetRequiredService<S7Driver>());
		services.AddSingleton<RecipeConverter>();
		services.AddSingleton(sp =>
		{
			var layout = sp.GetRequiredService<PlcConfiguration>().Layout;
			var registry = sp.GetRequiredService<RecipeMetadataRegistry>();
			return new ArrayCodec(layout.IntDb, layout.FloatDb, layout.StringDb, registry.GetStringMaxLength());
		});
		services.AddSingleton<PlcTransactionExecutor>();
		services.AddSingleton<PlcSyncCoordinator>();
		services.AddSingleton<IPlcSyncService>(sp => sp.GetRequiredService<PlcSyncCoordinator>());
		services.AddSingleton<S7Service>(sp =>
		{
			var transactionExecutor = sp.GetRequiredService<PlcTransactionExecutor>();
			var protocolSettings = sp.GetRequiredService<PlcProtocolSettings>();
			var plcConfiguration = sp.GetRequiredService<PlcConfiguration>();
			var driver = sp.GetRequiredService<S7Driver>();
			var monitorLogger = sp.GetRequiredService<ILogger<PlcExecutionMonitor>>();
			var serviceLogger = sp.GetRequiredService<ILogger<S7Service>>();

			S7Service? service = null;
			var monitor = new PlcExecutionMonitor(
				transactionExecutor,
				protocolSettings,
				// service is always assigned (line below) before this lambda can fire:
				// PlcExecutionMonitor only invokes onConnectionLost from its poll loop,
				// which starts only when S7Service.ConnectAsync is called — well after
				// the factory returns and service has been assigned.
				onConnectionLost: () => service!.OnConnectionLost(),
				monitorLogger);

			service = new S7Service(driver, monitor, transactionExecutor, plcConfiguration, serviceLogger);
			return service;
		});
		services.AddSingleton<IS7Connection>(sp => sp.GetRequiredService<S7Service>());
		services.AddSingleton<IS7Reader>(sp => sp.GetRequiredService<S7Service>());
		services.AddSingleton<IS7ExecutionStream>(sp => sp.GetRequiredService<S7Service>());

		services.AddSingleton(sp => sp.GetRequiredService<AppConfiguration>().PlcConfiguration);
		services.AddSingleton(sp => sp.GetRequiredService<AppConfiguration>().PlcConfiguration.ProtocolSettings);
		return services;
	}
}
