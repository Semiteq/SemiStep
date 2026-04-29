using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SemiStep.Core.Configuration;
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
		services.AddSingleton<LoopParser>();

		services.AddSingleton<IReadOnlyDictionary<int, CompiledFormula>>(_ => new Dictionary<int, CompiledFormula>());
		services.AddSingleton<FormulaEngine>();
		services.AddSingleton<FormulaApplicationCoordinator>();

		services.AddSingleton<PropertyParser>();

		services.AddSingleton(sp => new RecipeMetadataRegistry(sp.GetRequiredService<AppConfiguration>()));
		services.AddSingleton<RecipeStateManager>();
		services.AddSingleton<RecipeHistoryManager>();
		services.AddSingleton<ImportedRecipeValidator>();

		services.AddSingleton(sp => new RecipeWorkspace(
			sp.GetRequiredService<RecipeStateManager>(),
			sp.GetRequiredService<RecipeHistoryManager>(),
			sp.GetRequiredService<RecipeAnalyzer>(),
			sp.GetRequiredService<IPlcSyncService>(),
			sp.GetRequiredService<ILogger<RecipeWorkspace>>()));

		services.AddSingleton(sp => new RecipeEditor(
			sp.GetRequiredService<RecipeWorkspace>(),
			sp.GetRequiredService<RecipeMetadataRegistry>(),
			sp.GetRequiredService<FormulaApplicationCoordinator>(),
			sp.GetRequiredService<PropertyParser>()));

		services.AddSingleton(sp => new PlcLifecycleManager(
			sp.GetRequiredService<RecipeWorkspace>(),
			sp.GetRequiredService<IS7Connection>(),
			sp.GetRequiredService<IS7Reader>(),
			sp.GetRequiredService<IS7ExecutionStream>(),
			sp.GetRequiredService<IPlcSyncService>(),
			sp.GetRequiredService<ImportedRecipeValidator>(),
			sp.GetRequiredService<ILogger<PlcLifecycleManager>>()));

		return services;
	}
}
