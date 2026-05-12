using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Plc;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Formulas;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.State;

namespace SemiStep.Core.Recipes;

public static class RecipeDi
{
	public static IServiceCollection AddRecipe(this IServiceCollection services)
	{
		services.AddSingleton<RecipeAnalyzer>();

		services.AddSingleton<IReadOnlyDictionary<int, CompiledFormula>>(_ => new Dictionary<int, CompiledFormula>());
		services.AddSingleton<FormulaEngine>();
		services.AddSingleton<FormulaApplicationCoordinator>();

		services.AddSingleton<RecipeMetadataRegistry>();
		services.AddSingleton<RecipeStateManager>();
		services.AddSingleton<RecipeHistoryManager>();
		services.AddSingleton<ImportedRecipeValidator>();

		services.AddSingleton<RecipeWorkspace>();
		services.AddSingleton<RecipeEditor>();
		services.AddSingleton<PlcLifecycleManager>();

		return services;
	}
}
