using Domain.Ports;

using Microsoft.Extensions.DependencyInjection;

namespace S7;

public static class ModbusTcpDi
{
	public static IServiceCollection AddModbusTcp(this IServiceCollection services, string host = "127.0.0.1", int port = 502)
	{
		services.AddSingleton<IPlcConnection>(new ModbusTcpConnection(host, port));

		return services;
	}
}
