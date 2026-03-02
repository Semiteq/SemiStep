using Config.Facade;

using Microsoft.Extensions.DependencyInjection;

namespace Config;

public static class ConfigDi
{
	public static IServiceCollection AddConfig(this IServiceCollection services)
	{
		services.AddSingleton<ConfigFacade>();

		return services;
	}
}
