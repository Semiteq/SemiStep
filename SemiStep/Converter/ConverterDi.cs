using Microsoft.Extensions.DependencyInjection;

namespace Converter;

public static class ConverterDi
{
	public static IServiceCollection AddConverter(this IServiceCollection services)
	{
		services.AddSingleton<RecipeConverter>();

		return services;
	}
}
