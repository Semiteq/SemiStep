using Microsoft.Extensions.DependencyInjection;

using TypesShared.Domain;

namespace Csv;

public static class CsvDi
{
	public static IServiceCollection AddCsv(this IServiceCollection services)
	{
		services.AddSingleton<CsvRowConverter>();
		services.AddSingleton<CsvFileSerializer>();
		services.AddSingleton<ICsvService, CsvService>();

		return services;
	}
}
