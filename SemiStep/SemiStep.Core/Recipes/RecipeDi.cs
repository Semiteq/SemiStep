using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Plc;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Helpers;

namespace SemiStep.Core.Recipes;

public static class RecipeDi
{
	public static IServiceCollection AddRecipe(this IServiceCollection services)
	{
		services.AddSingleton<RecipeAnalyzer>();

		services.AddSingleton<RecipeMetadataRegistry>();
		services.AddSingleton<ImportedRecipeValidator>();

		services.AddSingleton<RecipeSession>();
		services.AddSingleton<PlcLifecycleManager>();

		return services;
	}
}
