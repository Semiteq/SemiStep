using Config.Facade;

using Microsoft.Extensions.DependencyInjection;

using Serilog;

namespace Config;

public static class ConfigDi
{
	public static IServiceCollection AddConfig(this IServiceCollection services, ILogger? logger = null)
	{
		if (logger is not null)
		{
			services.AddSingleton<ILogger>(logger);
		}

		services.AddSingleton<ConfigFacade>();

		return services;
	}
}
