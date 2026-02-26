using Domain.Ports;

using Microsoft.Extensions.DependencyInjection;

using Serilog;

namespace Csv;

public static class CsvDi
{
	public static IServiceCollection AddCsv(this IServiceCollection services)
	{
		services.AddSingleton<CsvSerializer>();
		services.AddSingleton<ICsvService, CsvCsvService>();

		return services;
	}
}
