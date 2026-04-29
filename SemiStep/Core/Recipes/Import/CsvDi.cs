using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SemiStep.Core.Recipes.Import;

public static class CsvDi
{
	public static IServiceCollection AddCsv(this IServiceCollection services)
	{
		services.AddSingleton<CsvRowConverter>();
		services.AddSingleton<CsvFileSerializer>();
		services.AddSingleton(sp => new CsvService(
			sp.GetRequiredService<CsvFileSerializer>(),
			sp.GetRequiredService<ILogger<CsvService>>()));

		return services;
	}
}
