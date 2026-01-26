using Microsoft.Extensions.DependencyInjection;

using Recipe.Analysis;
using Recipe.Services;

using Serilog;

namespace Recipe;

public static class RecipeDi
{
	public static IServiceCollection AddRecipe(this IServiceCollection services, ILogger? logger = null)
	{
		if (logger is not null)
		{
			services.AddSingleton(logger);
		}

		services.AddSingleton<StepFactory>();
		services.AddSingleton<RecipeMutator>();
		services.AddSingleton<PropertyValidator>();
		services.AddSingleton<LoopParser>();
		services.AddSingleton<TimingCalculator>();
		services.AddSingleton<RecipeAnalyzer>();

		return services;
	}
}
