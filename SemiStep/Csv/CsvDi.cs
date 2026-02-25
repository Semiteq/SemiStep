using Domain.Ports;

using Microsoft.Extensions.DependencyInjection;

using Serilog;

namespace Csv;

public static class CsvDi
{
	public static IServiceCollection AddCsv(this IServiceCollection services, ILogger? logger = null)
	{
		if (logger is not null)
		{
			services.AddSingleton(logger);
		}

		services.AddSingleton<CsvSerializer>();
		services.AddSingleton<IRecipeRepository, CsvRecipeRepository>();

		return services;
	}
}
