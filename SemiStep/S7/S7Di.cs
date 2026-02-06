using Domain.Ports;

using Microsoft.Extensions.DependencyInjection;

using S7.Facade;

namespace S7;

public static class S7Di
{
	public static IServiceCollection AddS7(this IServiceCollection services)
	{
		services.AddSingleton<IPlcConnection, S7Facade>();
		return services;
	}
}
