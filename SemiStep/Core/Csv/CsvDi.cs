using Microsoft.Extensions.DependencyInjection;

namespace Csv;

public static class CsvDi
{
	public static IServiceCollection AddCsv(this IServiceCollection services)
	{
		services.AddSingleton<CsvRowConverter>();
		services.AddSingleton<CsvFileSerializer>();
		services.AddSingleton(sp => new CsvService(sp.GetRequiredService<CsvFileSerializer>()));

		return services;
	}
}
