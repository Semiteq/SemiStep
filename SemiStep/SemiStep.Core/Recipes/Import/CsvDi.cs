using Microsoft.Extensions.DependencyInjection;

namespace SemiStep.Core.Recipes.Import;

public static class CsvDi
{
	public static IServiceCollection AddCsv(this IServiceCollection services)
	{
		services.AddSingleton<CsvRowConverter>();
		services.AddSingleton<CsvFileSerializer>();
		services.AddSingleton<CsvService>();

		return services;
	}
}
