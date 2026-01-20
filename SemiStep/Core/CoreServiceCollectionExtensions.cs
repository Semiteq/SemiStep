using Core.Analysis;
using Core.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Core;

public static class CoreServiceCollectionExtensions
{
	public static IServiceCollection AddCore(this IServiceCollection services)
	{
		services.AddSingleton<StepFactory>();
		services.AddSingleton<RecipeMutator>();
		services.AddSingleton<PropertyValidator>();
		services.AddSingleton<LoopParser>();
		services.AddSingleton<TimingCalculator>();
		services.AddSingleton<RecipeAnalyzer>();

		return services;
	}
}
