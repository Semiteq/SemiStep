using Domain.Ports;

using Microsoft.Extensions.DependencyInjection;

namespace Csv;

public static class CsvDi
{
	public static IServiceCollection AddCsv(this IServiceCollection services)
	{
		services.AddSingleton<IRecipeRepository, CsvRecipeRepository>();

		return services;
	}
}
